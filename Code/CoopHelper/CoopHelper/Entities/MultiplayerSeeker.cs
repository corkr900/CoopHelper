using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/MultiplayerSeeker")]
	[Tracked]
	public class MultiplayerSeeker : Actor, ISynchronizable {
		private struct PatrolPoint {
			public Vector2 Point;

			public float Distance;
		}

		[Pooled]
		private class RecoverBlast : Entity {
			private Sprite sprite;

			public override void Added(Scene scene) {
				base.Added(scene);
				base.Depth = -199;
				if (sprite == null) {
					Add(sprite = GFX.SpriteBank.Create("seekerShockWave"));
					sprite.OnLastFrame = delegate
					{
						RemoveSelf();
					};
				}
				sprite.Play("shockwave", restart: true);
			}

			public static void Spawn(Vector2 position) {
				RecoverBlast recoverBlast = Engine.Pooler.Create<RecoverBlast>();
				recoverBlast.Position = position;
				Engine.Scene.Add(recoverBlast);
			}
		}

		public static readonly Color TrailColor = Calc.HexToColor("99e550");

		private const int StIdle = 0;
		private const int StPatrol = 1;
		private const int StSpotted = 2;
		private const int StAttack = 3;
		private const int StStunned = 4;
		private const int StSkidding = 5;
		private const int StRegenerate = 6;
		private const int StReturned = 7;

		private const int size = 12;
		private const int bounceHeight = 4;
		private const float Accel = 600f;
		private const float WallCollideStunThreshold = 100f;
		private const float StunXSpeed = 100f;
		private const float BounceSpeed = 200f;
		private const float SightDistSq = 25600f;
		private const float ExplodeRadius = 40f;

		private const float FarDistSq = 12544f;
		private const float IdleAccel = 200f;
		private const float IdleSpeed = 50f;
		private const float PatrolSpeed = 25f;
		private const int PatrolChoices = 3;
		private const float PatrolWaitTime = 0.4f;

		private const float AttackWindUpSpeed = -60f;
		private const float AttackWindUpTime = 0.3f;
		private const float AttackStartSpeed = 180f;
		private const float AttackTargetSpeed = 260f;
		private const float AttackAccel = 300f;
		private const float DirectionDotThreshold = 0.4f;
		private const int AttackTargetUpShift = 2;
		private const float AttackMaxRotateRadians = 0.610865235f;

		private const float StunnedAccel = 150f;
		private const float StunTime = 0.8f;
		private const float SkiddingAccel = 200f;
		private const float StrongSkiddingAccel = 400f;
		private const float StrongSkiddingTime = 0.08f;

		private const float SpottedTargetSpeed = 60f;
		private const float SpottedFarSpeed = 90f;
		private const float SpottedMaxYDist = 24f;
		private const float AttackMinXDist = 16f;
		private const float SpottedLosePlayerTime = 0.6f;
		private const float SpottedMinAttackTime = 0.2f;

		private static PatrolPoint[] patrolChoices = new PatrolPoint[PatrolChoices];

		public Vector2 Speed;
		public VertexLight Light;

		internal Vector2[] patrolPoints;

		private Hitbox physicsHitbox;
		private Hitbox breakWallsHitbox;
		private Hitbox attackHitbox;
		private Hitbox bounceHitbox;
		private Circle pushRadius;
		private StateMachine State;
		private Vector2 lastSpottedAt;
		private Vector2 lastPathTo;
		private bool spotted;
		private bool canSeePlayer;
		private Collision onCollideH;
		private Collision onCollideV;
		private Random random;
		private Shaker shaker;
		private Wiggler scaleWiggler;
		private bool lastPathFound;
		private List<Vector2> path;
		private int pathIndex;
		private SineWave idleSineX;
		private SineWave idleSineY;
		private bool dead;
		private SoundSource boopedSfx;
		private SoundSource aggroSfx;
		private SoundSource reviveSfx;
		private Sprite sprite;
		private int facing = 1;
		private int spriteFacing = 1;
		private string nextSprite;
		private HoldableCollider theo;
		private HashSet<string> flipAnimations = new HashSet<string> { "flipMouth", "flipEyes", "skid" };
		private float patrolWaitTimer;
		private float spottedLosePlayerTimer;
		private float spottedTurnDelay;
		private float attackSpeed;
		private bool attackWindUp;
		private bool strongSkid;

		private EntityID id;

		private const double RecurringUpdateFrequency = 0.5;
		private long lastUpdateSent = 0;
		private int owner = 0;
		private bool claimingOwnership = false;
		private bool bounced = false;
		private Vector2 bouncePosition;
		private bool bouncedRemotely = false;
		private bool killedRemotely = false;

		public bool IsOwner {
			get {
				return CoopHelperModule.Session?.IsInCoopSession == true
				&& owner == CoopHelperModule.Session?.SessionRole;
			}
		}

		public bool Attacking {
			get {
				return State.State == StAttack && !attackWindUp;
			}
		}

		public bool Spotted {
			get {
				return State.State == StAttack || State.State == StSpotted;
			}
		}

		public bool Regenerating => State.State == StRegenerate;

		private Vector2 FollowTarget => lastSpottedAt - Vector2.UnitY * 2f;

		public MultiplayerSeeker(Vector2 position, Vector2[] patrolPoints)
			: base(position) {
			Depth = -200;
			this.patrolPoints = patrolPoints;
			Collider = (physicsHitbox = new Hitbox(6f, 6f, -3f, -3f));
			breakWallsHitbox = new Hitbox(6f, 14f, -3f, -7f);
			attackHitbox = new Hitbox(12f, 8f, -6f, -2f);
			bounceHitbox = new Hitbox(16f, 6f, -8f, -8f);
			pushRadius = new Circle(ExplodeRadius);
			Add(new PlayerCollider(OnAttackPlayer, attackHitbox));
			Add(new PlayerCollider(OnBouncePlayer, bounceHitbox));
			Add(shaker = new Shaker(on: false));
			Add(State = new StateMachine());
			State.SetCallbacks(StIdle, IdleUpdate, IdleCoroutine, GenericStateBegin);
			State.SetCallbacks(StPatrol, PatrolUpdate, null, PatrolBegin);
			State.SetCallbacks(StSpotted, SpottedUpdate, SpottedCoroutine, SpottedBegin);
			State.SetCallbacks(StAttack, AttackUpdate, AttackCoroutine, AttackBegin);
			State.SetCallbacks(StStunned, StunnedUpdate, StunnedCoroutine, GenericStateBegin);
			State.SetCallbacks(StSkidding, SkiddingUpdate, SkiddingCoroutine, SkiddingBegin, SkiddingEnd);
			State.SetCallbacks(StRegenerate, RegenerateUpdate, RegenerateCoroutine, RegenerateBegin, RegenerateEnd);
			State.SetCallbacks(StReturned, null, ReturnedCoroutine, GenericStateBegin);
			onCollideH = OnCollideH;
			onCollideV = OnCollideV;
			Add(idleSineX = new SineWave(0.5f, 0f));
			Add(idleSineY = new SineWave(0.7f, 0f));
			Add(Light = new VertexLight(Color.White, 1f, 32, 64));
			Add(theo = new HoldableCollider(OnHoldable, attackHitbox));
			Add(new MirrorReflection());
			path = new List<Vector2>();
			IgnoreJumpThrus = true;
			Add(sprite = GFX.SpriteBank.Create("seeker"));
			sprite.OnLastFrame = delegate (string f)
			{
				if (flipAnimations.Contains(f) && spriteFacing != facing) {
					spriteFacing = facing;
					if (nextSprite != null) {
						sprite.Play(nextSprite);
						nextSprite = null;
					}
				}
			};
			sprite.OnChange = delegate (string last, string next)
			{
				nextSprite = null;
				sprite.OnLastFrame(last);
			};
			SquishCallback = delegate (CollisionData d)
			{
				if (!dead && (killedRemotely || !TrySquishWiggle(d, 3, 3))) {
					Entity deathFXEntity = new Entity(Position);
					DeathEffect deathFXComponent = new DeathEffect(Color.HotPink, base.Center - Position) {
						OnEnd = delegate
						{
							deathFXEntity.RemoveSelf();
						}
					};
					deathFXEntity.Add(deathFXComponent);
					deathFXEntity.Depth = -1000000;
					Scene.Add(deathFXEntity);
					Audio.Play("event:/game/05_mirror_temple/seeker_death", Position);
					RemoveSelf();
					dead = true;
					if (!killedRemotely) EntityStateTracker.PostUpdate(this);
				}
			};
			scaleWiggler = Wiggler.Create(0.8f, 2f);
			Add(scaleWiggler);
			Add(boopedSfx = new SoundSource());
			Add(aggroSfx = new SoundSource());
			Add(reviveSfx = new SoundSource());
		}

		public MultiplayerSeeker(EntityData data, Vector2 offset)
			: this(data.Position + offset, data.NodesOffset(offset)) {
			id = new EntityID(data.Level.Name, data.ID);
		}

		private void GenericStateBegin() {
			if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			random = new Random(SceneAs<Level>().Session.LevelData.LoadSeed);
			EntityStateTracker.AddListener(this);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Awake(Scene scene) {
			base.Awake(scene);
			Player entity = base.Scene.Tracker.GetEntity<Player>();
			if (entity == null || base.X == entity.X) {
				SnapFacing(1f);
			}
			else {
				SnapFacing(Math.Sign(entity.X - base.X));
			}
		}

		public override bool IsRiding(JumpThru jumpThru) {
			return false;
		}

		public override bool IsRiding(Solid solid) {
			return false;
		}

		private void OnAttackPlayer(Player player) {
			if (State.State != StStunned) {
				player.Die((player.Center - Position).SafeNormalize());
				return;
			}
			Collider collider = Collider;
			Collider = bounceHitbox;
			player.PointBounce(Center);
			Speed = (Center - player.Center).SafeNormalize(100f);
			scaleWiggler.Start();
			Collider = collider;
		}

		private void OnBouncePlayer(Player player) {
			Collider collider = Collider;
			Collider = attackHitbox;
			if (CollideCheck(player)) {
				OnAttackPlayer(player);
			}
			else {
				player.Bounce(base.Top);
				GotBouncedOn(player);
			}
			Collider = collider;
		}

		private void GotBouncedOn(Entity entity) {
			Celeste.Freeze(0.15f);
			Speed = (Center - entity.Center).SafeNormalize(BounceSpeed);
			State.State = StRegenerate;
			sprite.Scale = new Vector2(1.4f, 0.6f);
			SceneAs<Level>().Particles.Emit(Seeker.P_Stomp, 8, Center - Vector2.UnitY * 5f, new Vector2(6f, 3f));
			if (!bouncedRemotely) {
				bouncePosition = entity.Center;
				bounced = true;
				EntityStateTracker.PostUpdate(this);
			}
			else {
				bouncedRemotely = false;
			}
		}

		public void HitSpring() {
			Speed.Y = -150f;
		}

		private bool CanSeePlayer(Player player) {
			if (player == null) {
				return false;
			}
			if (State.State != StSpotted && !SceneAs<Level>().InsideCamera(base.Center) && Vector2.DistanceSquared(base.Center, player.Center) > SightDistSq) {
				return false;
			}
			Vector2 vector = (player.Center - base.Center).Perpendicular().SafeNormalize(2f);
			if (!base.Scene.CollideCheck<Solid>(base.Center + vector, player.Center + vector)) {
				return !base.Scene.CollideCheck<Solid>(base.Center - vector, player.Center - vector);
			}
			return false;
		}

		public override void Update() {
			Light.Alpha = Calc.Approach(Light.Alpha, 1f, Engine.DeltaTime * 2f);
			foreach (Entity barrier in Scene.Tracker.GetEntities<SeekerBarrier>()) {
				barrier.Collidable = true;
			}
			sprite.Scale.X = Calc.Approach(sprite.Scale.X, 1f, 2f * Engine.DeltaTime);
			sprite.Scale.Y = Calc.Approach(sprite.Scale.Y, 1f, 2f * Engine.DeltaTime);
			if (State.State == StRegenerate) {
				canSeePlayer = false;
			}
			else if (IsOwner) {
				Player player = Scene.Tracker.GetEntity<Player>();
				canSeePlayer = CanSeePlayer(player);
				if (canSeePlayer) {
					spotted = true;
					lastSpottedAt = player.Center;
				}
			}
			if (lastPathTo != lastSpottedAt) {
				lastPathTo = lastSpottedAt;
				pathIndex = 0;
				lastPathFound = SceneAs<Level>().Pathfinder.Find(ref path, base.Center, FollowTarget);
			}
			base.Update();
			MoveH(Speed.X * Engine.DeltaTime, onCollideH);
			MoveV(Speed.Y * Engine.DeltaTime, onCollideV);
			Level level = SceneAs<Level>();
			if (Left < (float)level.Bounds.Left && Speed.X < 0f) {
				Left = level.Bounds.Left;
				onCollideH(CollisionData.Empty);
			}
			else if (Right > (float)level.Bounds.Right && Speed.X > 0f) {
				Right = level.Bounds.Right;
				onCollideH(CollisionData.Empty);
			}
			if (Top < (float)(level.Bounds.Top + -8) && Speed.Y < 0f) {
				Top = level.Bounds.Top + -8;
				onCollideV(CollisionData.Empty);
			}
			else if (Bottom > (float)level.Bounds.Bottom && Speed.Y > 0f) {
				Bottom = level.Bounds.Bottom;
				onCollideV(CollisionData.Empty);
			}
			foreach (SeekerCollider seekerCollider in Scene.Tracker.GetComponents<SeekerCollider>()) {
				seekerCollider.Check(this);
			}
			if (State.State == StAttack && Speed.X > 0f) {
				bounceHitbox.Width = 16f;
				bounceHitbox.Position.X = -10f;
			}
			else if (State.State == StAttack && Speed.Y < 0f) {
				bounceHitbox.Width = 16f;
				bounceHitbox.Position.X = -6f;
			}
			else {
				bounceHitbox.Width = 12f;
				bounceHitbox.Position.X = -6f;
			}
			foreach (Entity seekerBarrier in Scene.Tracker.GetEntities<SeekerBarrier>()) {
				seekerBarrier.Collidable = false;
			}
		}

		private void TurnFacing(float dir, string gotoSprite = null) {
			if (dir != 0f) {
				facing = Math.Sign(dir);
			}
			if (spriteFacing != facing) {
				if (State.State == StSkidding) {
					sprite.Play("skid");
				}
				else if (State.State == StAttack || State.State == StSpotted) {
					sprite.Play("flipMouth");
				}
				else {
					sprite.Play("flipEyes");
				}
				nextSprite = gotoSprite;
			}
			else if (gotoSprite != null) {
				sprite.Play(gotoSprite);
			}
		}

		private void SnapFacing(float dir) {
			if (dir != 0f) {
				spriteFacing = (facing = Math.Sign(dir));
			}
		}

		private void OnHoldable(Holdable holdable) {
			if (State.State != StRegenerate && holdable.Dangerous(theo)) {
				holdable.HitSeeker(this);
				State.State = StStunned;
				Speed = (base.Center - holdable.Entity.Center).SafeNormalize(120f);
				scaleWiggler.Start();
			}
			else if ((State.State == StAttack || State.State == StSkidding) && holdable.IsHeld) {
				holdable.Swat(theo, Math.Sign(Speed.X));
				State.State = StStunned;
				Speed = (base.Center - holdable.Entity.Center).SafeNormalize(120f);
				scaleWiggler.Start();
			}
		}

		public override void Render() {
			Vector2 position = Position;
			Position += shaker.Value;
			Vector2 scale = sprite.Scale;
			sprite.Scale *= 1f - 0.3f * scaleWiggler.Value;
			sprite.Scale.X *= spriteFacing;
			base.Render();
			Position = position;
			sprite.Scale = scale;
		}

		public override void DebugRender(Camera camera) {
			Collider collider = base.Collider;
			base.Collider = attackHitbox;
			attackHitbox.Render(camera, Color.Red);
			base.Collider = bounceHitbox;
			bounceHitbox.Render(camera, Color.Aqua);
			base.Collider = collider;
		}

		private void SlammedIntoWall(CollisionData data) {
			float direction;
			float x;
			if (data.Direction.X > 0f) {
				direction = (float)Math.PI;
				x = base.Right;
			}
			else {
				direction = 0f;
				x = base.Left;
			}
			SceneAs<Level>().Particles.Emit(Seeker.P_HitWall, size, new Vector2(x, base.Y), Vector2.UnitY * 4f, direction);
			if (data.Hit is DashSwitch) {
				(data.Hit as DashSwitch).OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));
			}
			base.Collider = breakWallsHitbox;
			foreach (TempleCrackedBlock entity in base.Scene.Tracker.GetEntities<TempleCrackedBlock>()) {
				if (CollideCheck(entity, Position + Vector2.UnitX * Math.Sign(Speed.X))) {
					entity.Break(base.Center);
				}
			}
			base.Collider = physicsHitbox;
			SceneAs<Level>().DirectionalShake(Vector2.UnitX * Math.Sign(Speed.X));
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
			Speed.X = (float)Math.Sign(Speed.X) * -StunXSpeed;
			Speed.Y *= 0.4f;
			sprite.Scale.X = 0.6f;
			sprite.Scale.Y = 1.4f;
			shaker.ShakeFor(0.5f, removeOnFinish: false);
			scaleWiggler.Start();
			State.State = StStunned;
			if (data.Hit is SeekerBarrier) {
				(data.Hit as SeekerBarrier).OnReflectSeeker();
				Audio.Play("event:/game/05_mirror_temple/seeker_hit_lightwall", Position);
			}
			else {
				Audio.Play("event:/game/05_mirror_temple/seeker_hit_normal", Position);
			}
		}

		private void OnCollideH(CollisionData data) {
			if (State.State == StAttack && data.Hit != null) {
				int num = Math.Sign(Speed.X);
				if ((!CollideCheck<Solid>(Position + new Vector2(num, 4f)) && !MoveVExact(bounceHeight)) || (!CollideCheck<Solid>(Position + new Vector2(num, -4f)) && !MoveVExact(-bounceHeight))) {
					return;
				}
			}
			if ((State.State == StAttack || State.State == StSkidding) && Math.Abs(Speed.X) >= WallCollideStunThreshold) {
				SlammedIntoWall(data);
			}
			else {
				Speed.X *= -0.2f;
			}
		}

		private void OnCollideV(CollisionData data) {
			if (State.State == StAttack) {
				Speed.Y *= -0.6f;
			}
			else {
				Speed.Y *= -0.2f;
			}
		}

		private void CreateTrail() {
			Vector2 scale = sprite.Scale;
			sprite.Scale *= 1f - 0.3f * scaleWiggler.Value;
			sprite.Scale.X *= spriteFacing;
			TrailManager.Add(this, TrailColor, 0.5f, frozenUpdate: false, useRawDeltaTime: false);
			sprite.Scale = scale;
		}

		private int IdleUpdate() {
			if (canSeePlayer) {
				return StSpotted;
			}
			Vector2 vector = Vector2.Zero;
			if (spotted && Vector2.DistanceSquared(Center, FollowTarget) > 64f) {
				float speedMagnitude = GetSpeedMagnitude(IdleSpeed);
				vector = ((!lastPathFound) ? (FollowTarget - Center).SafeNormalize(speedMagnitude) : GetPathSpeed(speedMagnitude));
			}
			if (vector == Vector2.Zero) {
				vector.X = idleSineX.Value * 6f;
				vector.Y = idleSineY.Value * 6f;
			}
			Speed = Calc.Approach(Speed, vector, IdleAccel * Engine.DeltaTime);
			if (Speed.LengthSquared() > 400f) {
				TurnFacing(Speed.X);
			}
			if (spriteFacing == facing) {
				sprite.Play("idle");
			}
			return StIdle;
		}

		private IEnumerator IdleCoroutine() {
			if (patrolPoints != null && patrolPoints.Length != 0 && spotted) {
				while (Vector2.DistanceSquared(Center, FollowTarget) > 64f) {
					yield return null;
				}
				yield return 0.3f;
				State.State = StPatrol;
			}
		}

		private Vector2 GetPathSpeed(float magnitude) {
			if (pathIndex >= path.Count) {
				return Vector2.Zero;
			}
			if (Vector2.DistanceSquared(base.Center, path[pathIndex]) < 36f) {
				pathIndex++;
				return GetPathSpeed(magnitude);
			}
			return (path[pathIndex] - base.Center).SafeNormalize(magnitude);
		}

		private float GetSpeedMagnitude(float baseMagnitude) {
			Player player = Scene.Tracker.GetEntity<Player>();
			if (player != null) {
				Vector2 playerPos = IsOwner ? (player.Center) : lastSpottedAt;
				return Vector2.DistanceSquared(Center, playerPos) > FarDistSq ? baseMagnitude * 3f : baseMagnitude * 1.5f;
			}
			return baseMagnitude;
		}

		private void PatrolBegin() {
			State.State = ChoosePatrolTarget();
			patrolWaitTimer = 0f;
			if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		private int PatrolUpdate() {
			if (canSeePlayer) {
				return StSpotted;
			}
			if (patrolWaitTimer > 0f) {
				patrolWaitTimer -= Engine.DeltaTime;
				if (patrolWaitTimer <= 0f) {
					return ChoosePatrolTarget();
				}
			}
			else if (Vector2.DistanceSquared(base.Center, lastSpottedAt) < 144f) {
				patrolWaitTimer = PatrolWaitTime;
			}
			float speedMagnitude = GetSpeedMagnitude(PatrolSpeed);
			Speed = Calc.Approach(target: (!lastPathFound) ? (FollowTarget - base.Center).SafeNormalize(speedMagnitude) : GetPathSpeed(speedMagnitude), val: Speed, maxMove: Accel * Engine.DeltaTime);
			if (Speed.LengthSquared() > 100f) {
				TurnFacing(Speed.X);
			}
			if (spriteFacing == facing) {
				sprite.Play("search");
			}
			return StPatrol;
		}

		private int ChoosePatrolTarget() {
			Player player = Scene.Tracker.GetEntity<Player>();
			if (player == null) {
				return StIdle;
			}
			for (int i = 0; i < PatrolChoices; i++) {
				patrolChoices[i].Distance = 0f;
			}
			int numVisiblePoints = 0;
			foreach (Vector2 patrolPt in patrolPoints) {
				if (Vector2.DistanceSquared(Center, patrolPt) < SpottedMaxYDist * SpottedMaxYDist) {
					continue;
				}
				Vector2 playerPos = IsOwner ? player.Center : lastSpottedAt;
				float playerDistSqr = Vector2.DistanceSquared(patrolPt, playerPos);
				for (int k = 0; k < PatrolChoices; k++) {
					if (playerDistSqr < patrolChoices[k].Distance || patrolChoices[k].Distance <= 0f) {
						numVisiblePoints++;
						for (int targetChoice = AttackTargetUpShift; targetChoice > k; targetChoice--) {
							patrolChoices[targetChoice].Distance = patrolChoices[targetChoice - 1].Distance;
							patrolChoices[targetChoice].Point = patrolChoices[targetChoice - 1].Point;
						}
						patrolChoices[k].Distance = playerDistSqr;
						patrolChoices[k].Point = patrolPt;
						break;
					}
				}
			}
			if (numVisiblePoints <= 0) {
				return StIdle;
			}
			lastSpottedAt = patrolChoices[random.Next(Math.Min(PatrolChoices, numVisiblePoints))].Point;
			lastPathTo = lastSpottedAt;
			pathIndex = 0;
			lastPathFound = SceneAs<Level>().Pathfinder.Find(ref path, Center, FollowTarget);
			if (IsOwner) EntityStateTracker.PostUpdate(this);
			return StPatrol;
		}

		private void SpottedBegin() {
			aggroSfx.Play("event:/game/05_mirror_temple/seeker_aggro");
			if (IsOwner) {
				Player entity = Scene.Tracker.GetEntity<Player>();
				if (entity != null) {
					TurnFacing(entity.X - X, "spot");
				}
			}
			spottedLosePlayerTimer = SpottedLosePlayerTime;
			spottedTurnDelay = 1f;
			if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		private int SpottedUpdate() {
			if (!canSeePlayer) {
				spottedLosePlayerTimer -= Engine.DeltaTime;
				if (spottedLosePlayerTimer < 0f) {
					return StIdle;
				}
			}
			else {
				spottedLosePlayerTimer = SpottedLosePlayerTime;
			}
			float speedMagnitude = GetSpeedMagnitude(SpottedTargetSpeed);
			Vector2 vector = (!lastPathFound) ? (FollowTarget - Center).SafeNormalize(speedMagnitude) : GetPathSpeed(speedMagnitude);
			if (Vector2.DistanceSquared(Center, FollowTarget) < 2500f && Y < FollowTarget.Y) {
				float num = vector.Angle();
				if (Y < FollowTarget.Y - 2f) {
					num = Calc.AngleLerp(num, (float)Math.PI / 2f, 0.5f);
				}
				else if (Y > FollowTarget.Y + 2f) {
					num = Calc.AngleLerp(num, -(float)Math.PI / 2f, 0.5f);
				}
				vector = Calc.AngleToVector(num, SpottedTargetSpeed);
				Vector2 vector2 = Vector2.UnitX * Math.Sign(X - lastSpottedAt.X) * 48f;
				if (Math.Abs(X - lastSpottedAt.X) < 36f && !CollideCheck<Solid>(Position + vector2) && !CollideCheck<Solid>(lastSpottedAt + vector2)) {
					vector.X = Math.Sign(X - lastSpottedAt.X) * 60;
				}
			}
			Speed = Calc.Approach(Speed, vector, Accel * Engine.DeltaTime);
			spottedTurnDelay -= Engine.DeltaTime;
			if (spottedTurnDelay <= 0f) {
				TurnFacing(Speed.X, "spotted");
			}
			return StSpotted;
		}

		private IEnumerator SpottedCoroutine() {
			yield return SpottedMinAttackTime;
			while (!CanAttack()) {
				yield return null;
			}
			State.State = StAttack;
		}

		private bool CanAttack() {
			if (Math.Abs(base.Y - lastSpottedAt.Y) > SpottedMaxYDist) {
				return false;
			}
			if (Math.Abs(base.X - lastSpottedAt.X) < AttackMinXDist) {
				return false;
			}
			Vector2 value = (FollowTarget - base.Center).SafeNormalize();
			if (Vector2.Dot(-Vector2.UnitY, value) > 0.5f || Vector2.Dot(Vector2.UnitY, value) > 0.5f) {
				return false;
			}
			if (CollideCheck<Solid>(Position + Vector2.UnitX * Math.Sign(lastSpottedAt.X - base.X) * 24f)) {
				return false;
			}
			return true;
		}

		private void AttackBegin() {
			Audio.Play("event:/game/05_mirror_temple/seeker_dash", Position);
			attackWindUp = true;
			attackSpeed = AttackWindUpSpeed;
			Speed = (FollowTarget - Center).SafeNormalize(AttackWindUpSpeed);
			if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		private int AttackUpdate() {
			if (!attackWindUp) {
				Vector2 vector = (FollowTarget - base.Center).SafeNormalize();
				if (Vector2.Dot(Speed.SafeNormalize(), vector) < DirectionDotThreshold) {
					return StSkidding;
				}
				attackSpeed = Calc.Approach(attackSpeed, AttackTargetSpeed, AttackAccel * Engine.DeltaTime);
				Speed = Speed.RotateTowards(vector.Angle(), AttackMaxRotateRadians * Engine.DeltaTime).SafeNormalize(attackSpeed);
				if (base.Scene.OnInterval(0.04f)) {
					Vector2 vector2 = (-Speed).SafeNormalize();
					SceneAs<Level>().Particles.Emit(Seeker.P_Attack, 2, Position + vector2 * 4f, Vector2.One * 4f, vector2.Angle());
				}
				if (base.Scene.OnInterval(0.06f)) {
					CreateTrail();
				}
			}
			return StAttack;
		}

		private IEnumerator AttackCoroutine() {
			TurnFacing(lastSpottedAt.X - X, "windUp");
			yield return AttackWindUpTime;
			attackWindUp = false;
			attackSpeed = 180f;
			Speed = (lastSpottedAt - Vector2.UnitY * 2f - Center).SafeNormalize(AttackStartSpeed);
			SnapFacing(Speed.X);
		}

		private int StunnedUpdate() {
			Speed = Calc.Approach(Speed, Vector2.Zero, StunnedAccel * Engine.DeltaTime);
			return StStunned;
		}

		private IEnumerator StunnedCoroutine() {
			yield return StunTime;
			State.State = StIdle;
		}

		private void SkiddingBegin() {
			Audio.Play("event:/game/05_mirror_temple/seeker_dash_turn", Position);
			strongSkid = false;
			TurnFacing(-facing);
			if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		private int SkiddingUpdate() {
			Speed = Calc.Approach(Speed, Vector2.Zero, (strongSkid ? StrongSkiddingAccel : SkiddingAccel) * Engine.DeltaTime);
			if (Speed.LengthSquared() < 400f) {
				if (canSeePlayer) {
					return StSpotted;
				}
				return StIdle;
			}
			return StSkidding;
		}

		private IEnumerator SkiddingCoroutine() {
			yield return StrongSkiddingTime;
			strongSkid = true;
		}

		private void SkiddingEnd() {
			spriteFacing = facing;
		}

		private void RegenerateBegin() {
			Audio.Play("event:/game/general/thing_booped", Position);
			boopedSfx.Play("event:/game/05_mirror_temple/seeker_booped");
			sprite.Play("takeHit");
			Collidable = false;
			State.Locked = true;
			Light.StartRadius = 16f;
			Light.EndRadius = 32f;
			//if (IsOwner) EntityStateTracker.PostUpdate(this);
		}

		private void RegenerateEnd() {
			reviveSfx.Play("event:/game/05_mirror_temple/seeker_revive");
			Collidable = true;
			Light.StartRadius = 32f;
			Light.EndRadius = 64f;
		}

		private int RegenerateUpdate() {
			Speed.X = Calc.Approach(Speed.X, 0f, 150f * Engine.DeltaTime);
			Speed = Calc.Approach(Speed, Vector2.Zero, 150f * Engine.DeltaTime);
			return StRegenerate;
		}

		private IEnumerator RegenerateCoroutine() {
			yield return 1f;
			shaker.On = true;
			yield return 0.2f;
			sprite.Play("pulse");
			yield return 0.5f;
			sprite.Play("recover");
			RecoverBlast.Spawn(Position);
			yield return 0.15f;
			Collider = pushRadius;
			Player player = CollideFirst<Player>();
			if (player != null && !Scene.CollideCheck<Solid>(Position, player.Center)) {
				player.ExplodeLaunch(Position, true, false);
			}
			TheoCrystal theoCrystal = CollideFirst<TheoCrystal>();
			if (theoCrystal != null && !Scene.CollideCheck<Solid>(Position, theoCrystal.Center)) {
				theoCrystal.ExplodeLaunch(Position);
			}
			foreach (TempleCrackedBlock entity in Scene.Tracker.GetEntities<TempleCrackedBlock>()) {
				if (CollideCheck(entity)) {
					entity.Break(Position);
				}
			}
			foreach (TouchSwitch entity2 in Scene.Tracker.GetEntities<TouchSwitch>()) {
				if (CollideCheck(entity2)) {
					entity2.TurnOn();
				}
			}
			Collider = physicsHitbox;
			Level level = SceneAs<Level>();
			level.Displacement.AddBurst(Position, 0.4f, 12f, 36f, 0.5f);
			level.Displacement.AddBurst(Position, 0.4f, 24f, 48f, 0.5f);
			level.Displacement.AddBurst(Position, 0.4f, 36f, 60f, 0.5f);
			for (float num = 0f; num < (float)Math.PI * 2f; num += 0.17453292f) {
				Vector2 position = Center + Calc.AngleToVector(num + Calc.Random.Range(-(float)Math.PI / 90f, (float)Math.PI / 90f), Calc.Random.Range(size, 18));
				level.Particles.Emit(Seeker.P_Regen, position, num);
			}
			shaker.On = false;
			State.Locked = false;
			State.State = StReturned;
		}

		private IEnumerator ReturnedCoroutine() {
			yield return 0.3f;
			State.State = StIdle;
		}





		public static int GetHeader() => 17;

		public static MultiplayerSeekerState ParseState(CelesteNetBinaryReader r) {
			MultiplayerSeekerState s = new MultiplayerSeekerState();
			s.Dead = r.ReadBoolean();
			s.Bounced = r.ReadBoolean();
			s.Spotted = r.ReadBoolean();
			s.CanSeePlayer = r.ReadBoolean();
			s.LastPathFound = r.ReadBoolean();

			s.NewOwner = r.ReadInt32();
			s.StateID = r.ReadInt32();
			s.Facing = r.ReadInt32();
			s.PathIndex = r.ReadInt32();

			s.Position = r.ReadVector2();
			s.Speed = r.ReadVector2();
			s.LastSpottedAt = r.ReadVector2();
			s.LastPathTo = r.ReadVector2();
			s.BouncePosition = r.ReadVector2();

			int count = r.ReadInt32();
			List<Vector2> path = new List<Vector2>(count);
			for (int i = 0; i < count; i++) {
				path.Add(r.ReadVector2());
			}
			s.Path = path;

			return s;
		}

		public EntityID GetID() => id;

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(dead);
			w.Write(bounced);
			bounced = false;
			w.Write(spotted);
			w.Write(canSeePlayer);
			w.Write(lastPathFound);

			if (claimingOwnership) {
				w.Write(CoopHelperModule.Session?.IsInCoopSession == true ? CoopHelperModule.Session.SessionRole : -1);
			}
			else {
				w.Write(-1);
			}
			w.Write(State.State);
			w.Write(facing);
			w.Write(pathIndex);

			w.Write(Position);
			w.Write(Speed);
			w.Write(lastSpottedAt);
			w.Write(lastPathTo);
			w.Write(bouncePosition);

			if (path == null) {
				w.Write(0);
			}
			else {
				w.Write(path.Count);
				for (int i = 0; i < path.Count; i++) {
					w.Write(path[i]);
				}
			}

			lastUpdateSent = SaveData.Instance.Time;
		}

		public void ApplyState(object stateRaw) {
			if (stateRaw is MultiplayerSeekerState st) {
				spotted = st.Spotted;
				canSeePlayer = st.CanSeePlayer;
				lastPathFound = st.LastPathFound;
				facing = st.Facing;
				pathIndex = st.PathIndex;
				Position = st.Position;
				Speed = st.Speed;
				lastSpottedAt = st.LastSpottedAt;
				lastPathTo = st.LastPathTo;
				path = st.Path;

				if (st.Dead && !dead) {
					killedRemotely = true;
					SquishCallback(new CollisionData());
				}
				else if (st.Bounced) {
					bouncedRemotely = true;
					GotBouncedOn(new Entity() { Center = st.BouncePosition });
				}
				else if (State.State != st.StateID) {
					State.State = st.StateID;
				}
				// TODO switch ownership
			}
		}

		public bool CheckRecurringUpdate() {
			return CoopHelperModule.Session?.IsInCoopSession == true
				&& owner == CoopHelperModule.Session.SessionRole
				&& State.State != StIdle
				&& Util.TimeToSeconds((SaveData.Instance?.Time ?? 0) - lastUpdateSent) > RecurringUpdateFrequency;
		}
	}

	public static class MultiplayerSeekerExtensions {
		public static void Check(this SeekerCollider self, MultiplayerSeeker seeker) {
			if (self.OnCollide != null) {
				Collider collider = self.Entity.Collider;
				if (self.Collider != null) {
					self.Entity.Collider = self.Collider;
				}
				if (seeker.CollideCheck(self.Entity)) {
					self.OnCollide(new Seeker(seeker.Position, seeker.patrolPoints));
				}
				self.Entity.Collider = collider;
			}
		}

		public static void HitSeeker(this Holdable self, MultiplayerSeeker seeker) {
			if (self.OnHitSeeker != null) {
				self.OnHitSeeker(new Seeker(seeker.Position, seeker.patrolPoints));
			}
		}
	}

	public class MultiplayerSeekerState {
		public bool Dead;
		public bool Bounced;
		public bool Spotted;
		public bool CanSeePlayer;
		public bool LastPathFound;

		public int NewOwner;
		public int StateID;
		public int Facing;
		public int PathIndex;

		public Vector2 Position;
		public Vector2 Speed;
		public Vector2 LastSpottedAt;
		public Vector2 LastPathTo;
		public Vector2 BouncePosition;

		public List<Vector2> Path;
	}
}
