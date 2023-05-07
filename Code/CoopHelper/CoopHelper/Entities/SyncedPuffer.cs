using Celeste.Mod.CelesteNet;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Entities {
	[CustomEntity("corkr900CoopHelper/SyncedPuffer")]
	public class SyncedPuffer : Actor, ISynchronizable {
		public enum States {
			Idle = 0,
			Hit = 1,
			Gone = 2,
		}

		private const float RespawnTime = 2.5f;
		private const float RespawnMoveTime = 0.5f;
		private const float BounceSpeed = 200f;
		private const float ExplodeRadius = 40f;
		private const float DetectRadius = 32f;
		private const float StunnedAccel = 320f;
		private const float AlertedRadius = 60f;
		private const float CantExplodeTime = 0.5f;

		private Sprite sprite;
		private States state;
		private Vector2 startPosition;
		private Vector2 anchorPosition;
		private Vector2 lastSpeedPosition;
		private Vector2 lastSinePosition;
		private Circle pushRadius;
		private Circle detectRadius;
		private SineWave idleSine;
		private Vector2 hitSpeed;
		private float goneTimer;
		private float cannotHitTimer;
		private Collision onCollideV;
		private Collision onCollideH;
		private float alertTimer;
		private Wiggler bounceWiggler;
		private Wiggler inflateWiggler;
		private Vector2 scale;
		private SimpleCurve returnCurve;
		private float cantExplodeTimer;
		private Vector2 lastPlayerPos;
		private float playerAliveFade;
		private Vector2 facing = Vector2.One;
		private float eyeSpin;
		private EntityID id;
		private bool applyingRemoteState = false;

		private SyncedPuffer(Vector2 position, bool faceRight, bool isStatic, string spriteOverride, EntityID entID)
			: base(position) {
			id = entID;
			Collider = new Hitbox(12f, 10f, -6f, -5f);
			Add(new PlayerCollider(OnPlayer, new Hitbox(14f, 12f, -7f, -7f)));
			if (string.IsNullOrEmpty(spriteOverride)) spriteOverride = "pufferFish";
			Add(sprite = GFX.SpriteBank.Create(spriteOverride));
			sprite.Play("idle");
			if (!faceRight) {
				facing.X = -1f;
			}
			if (isStatic) {
				anchorPosition = Position;
			}
			else {
				idleSine = new SineWave(0.5f, 0f);
				idleSine.Randomize();
				Add(idleSine);
				anchorPosition = Position;
				Position += new Vector2(idleSine.Value * 3f, idleSine.ValueOverTwo * 2f);
			}
			state = States.Idle;
			startPosition = (lastSinePosition = (lastSpeedPosition = Position));
			pushRadius = new Circle(ExplodeRadius);
			detectRadius = new Circle(DetectRadius);
			onCollideV = OnCollideV;
			onCollideH = OnCollideH;
			scale = Vector2.One;
			bounceWiggler = Wiggler.Create(0.6f, 2.5f, delegate (float v) {
				sprite.Rotation = v * 20f * ((float)Math.PI / 180f);
			});
			Add(bounceWiggler);
			inflateWiggler = Wiggler.Create(0.6f, 2f);
			Add(inflateWiggler);
		}

		public SyncedPuffer(EntityData data, Vector2 offset) : this(
			data.Position + offset,
			data.Bool("right"),
			data.Bool("static", false),
			data.Attr("sprite", "pufferFish"),
			new EntityID(data.Level.Name, data.ID)) { }

		public override bool IsRiding(JumpThru jumpThru) {
			return false;
		}

		public override bool IsRiding(Solid solid) {
			return false;
		}

		public override void OnSquish(CollisionData data) {
			Explode();
			GotoGone();
		}

		private void OnCollideH(CollisionData data) {
			hitSpeed.X *= -0.8f;
		}

		private void OnCollideV(CollisionData data) {
			if (!(data.Direction.Y > 0f)) {
				return;
			}
			for (int i = -1; i <= 1; i += 2) {
				for (int j = 1; j <= 2; j++) {
					Vector2 vector = Position + Vector2.UnitX * j * i;
					if (!CollideCheck<Solid>(vector) && !OnGround(vector)) {
						Position = vector;
						return;
					}
				}
			}
			hitSpeed.Y *= -0.2f;
		}

		private void GotoIdle() {
			if (state == States.Gone) {
				Position = startPosition;
				cantExplodeTimer = CantExplodeTime;
				sprite.Play("recover");
				Audio.Play("event:/new_content/game/10_farewell/puffer_reform", Position);
			}
			lastSinePosition = (lastSpeedPosition = (anchorPosition = Position));
			hitSpeed = Vector2.Zero;
			idleSine?.Reset();
			state = States.Idle;
		}

		private void GotoHit(Vector2 from) {
			scale = new Vector2(1.2f, 0.8f);
			hitSpeed = Vector2.UnitY * BounceSpeed;
			state = States.Hit;
			bounceWiggler.Start();
			Alert(restart: true, playSfx: false);
			Audio.Play("event:/new_content/game/10_farewell/puffer_boop", Position);
			if (!applyingRemoteState) EntityStateTracker.PostUpdate(this);
		}

		private void GotoHitSpeed(Vector2 speed) {
			hitSpeed = speed;
			state = States.Hit;
			// This is only called on spring hit which is already physics based... Don't post update
			//if (!applyingRemoteState) EntityStateTracker.PostUpdate(this);
		}

		private void GotoGone() {
			Vector2 control = Position + (startPosition - Position) * 0.5f;
			if ((startPosition - Position).LengthSquared() > 100f) {
				if (Math.Abs(Position.Y - startPosition.Y) > Math.Abs(Position.X - startPosition.X)) {
					if (Position.X > startPosition.X) {
						control += Vector2.UnitX * -24f;
					}
					else {
						control += Vector2.UnitX * 24f;
					}
				}
				else if (Position.Y > startPosition.Y) {
					control += Vector2.UnitY * -24f;
				}
				else {
					control += Vector2.UnitY * 24f;
				}
			}
			returnCurve = new SimpleCurve(Position, startPosition, control);
			Collidable = false;
			goneTimer = RespawnTime;
			state = States.Gone;
			if (!applyingRemoteState) EntityStateTracker.PostUpdate(this);
		}

		private void Explode() {
			Collider collider = Collider;
			Collider = pushRadius;
			Audio.Play("event:/new_content/game/10_farewell/puffer_splode", Position);
			sprite.Play("explode");
			Player player = CollideFirst<Player>();
			if (player != null && !Scene.CollideCheck<Solid>(Position, player.Center)) {
				player.ExplodeLaunch(Position, snapUp: false, sidesOnly: true);
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
			foreach (FloatingDebris entity3 in Scene.Tracker.GetEntities<FloatingDebris>()) {
				if (CollideCheck(entity3)) {
					entity3.OnExplode(Position);
				}
			}
			Collider = collider;
			Level level = SceneAs<Level>();
			level.Shake();
			level.Displacement.AddBurst(Position, 0.4f, 12f, 36f, 0.5f);
			level.Displacement.AddBurst(Position, 0.4f, 24f, 48f, 0.5f);
			level.Displacement.AddBurst(Position, 0.4f, 36f, 60f, 0.5f);
			for (float num = 0f; num < (float)Math.PI * 2f; num += 0.17453292f) {
				Vector2 position = Center + Calc.AngleToVector(num + Calc.Random.Range(-(float)Math.PI / 90f, (float)Math.PI / 90f), Calc.Random.Range(12, 18));
				level.Particles.Emit(Seeker.P_Regen, position, num);
			}
		}

		public override void Render() {
			sprite.Scale = scale * (1f + inflateWiggler.Value * 0.4f);
			sprite.Scale *= facing;
			bool flag = false;
			if (sprite.CurrentAnimationID != "hidden" && sprite.CurrentAnimationID != "explode" && sprite.CurrentAnimationID != "recover") {
				flag = true;
			}
			else if (sprite.CurrentAnimationID == "explode" && sprite.CurrentAnimationFrame <= 1) {
				flag = true;
			}
			else if (sprite.CurrentAnimationID == "recover" && sprite.CurrentAnimationFrame >= 4) {
				flag = true;
			}
			if (flag) {
				sprite.DrawSimpleOutline();
			}
			float num = playerAliveFade * Calc.ClampedMap((Position - lastPlayerPos).Length(), 128f, 96f);
			if (num > 0f && state != States.Gone) {
				bool flag2 = false;
				Vector2 vector = lastPlayerPos;
				if (vector.Y < Y) {
					vector.Y = Y - (vector.Y - Y) * 0.5f;
					vector.X += vector.X - X;
					flag2 = true;
				}
				float radiansB = (vector - Position).Angle();
				for (int i = 0; i < 28; i++) {
					float num2 = (float)Math.Sin(Scene.TimeActive * 0.5f) * 0.02f;
					float num3 = Calc.Map((float)i / 28f + num2, 0f, 1f, -(float)Math.PI / 30f, 3.24631262f);
					num3 += bounceWiggler.Value * 20f * ((float)Math.PI / 180f);
					Vector2 arcDirection = Calc.AngleToVector(num3, 1f);
					Vector2 arcPoint = Position + arcDirection * DetectRadius;
					float t = Calc.ClampedMap(Calc.AbsAngleDiff(num3, radiansB), (float)Math.PI / 2f, 0.17453292f);
					t = Ease.CubeOut(t) * 0.8f * num;
					if (!(t > 0f)) {
						continue;
					}
					if (i == 0 || i == 27) {
						Draw.Line(arcPoint, arcPoint - arcDirection * 10f, Color.White * t);
						continue;
					}
					Vector2 vector4 = arcDirection * (float)Math.Sin(Scene.TimeActive * 2f + (float)i * 0.6f);
					if (i % 2 == 0) {
						vector4 *= -1f;
					}
					arcPoint += vector4;
					if (!flag2 && Calc.AbsAngleDiff(num3, radiansB) <= 0.17453292f) {
						Draw.Line(arcPoint, arcPoint - arcDirection * 3f, Color.White * t);
					}
					else {
						Draw.Point(arcPoint, Color.White * t);
					}
				}
			}
			base.Render();
			if (sprite.CurrentAnimationID == "alerted") {
				Vector2 vector5 = Position + new Vector2(3f, (facing.X < 0f) ? (-5) : (-4)) * sprite.Scale;
				Vector2 to = lastPlayerPos + new Vector2(0f, -4f);
				Vector2 vector6 = Calc.AngleToVector(Calc.Angle(vector5, to) + eyeSpin * ((float)Math.PI * 2f) * 2f, 1f);
				Vector2 vector7 = vector5 + new Vector2((float)Math.Round(vector6.X), (float)Math.Round(Calc.ClampedMap(vector6.Y, -1f, 1f, -1f, 2f)));
				Draw.Rect(vector7.X, vector7.Y, 1f, 1f, Color.Black);
			}
			sprite.Scale /= facing;
		}

		public override void Update() {
			base.Update();
			eyeSpin = Calc.Approach(eyeSpin, 0f, Engine.DeltaTime * 1.5f);
			scale = Calc.Approach(scale, Vector2.One, 1f * Engine.DeltaTime);
			if (cannotHitTimer > 0f) {
				cannotHitTimer -= Engine.DeltaTime;
			}
			if (state != States.Gone && cantExplodeTimer > 0f) {
				cantExplodeTimer -= Engine.DeltaTime;
			}
			if (alertTimer > 0f) {
				alertTimer -= Engine.DeltaTime;
			}
			Player entity = Scene.Tracker.GetEntity<Player>();
			if (entity == null) {
				playerAliveFade = Calc.Approach(playerAliveFade, 0f, 1f * Engine.DeltaTime);
			}
			else {
				playerAliveFade = Calc.Approach(playerAliveFade, 1f, 1f * Engine.DeltaTime);
				lastPlayerPos = entity.Center;
			}
			switch (state) {
				case States.Idle: {
					if (Position != lastSinePosition) {
						anchorPosition += Position - lastSinePosition;
					}
					Vector2 vector = anchorPosition + (idleSine == null ? Vector2.Zero : new Vector2(idleSine.Value * 3f, idleSine.ValueOverTwo * 2f));
					MoveToX(vector.X);
					MoveToY(vector.Y);
					lastSinePosition = Position;
					if (ProximityExplodeCheck()) {
						Explode();
						GotoGone();
						break;
					}
					if (AlertedCheck()) {
						Alert(restart: false, playSfx: true);
					}
					else if (sprite.CurrentAnimationID == "alerted" && alertTimer <= 0f) {
						Audio.Play("event:/new_content/game/10_farewell/puffer_shrink", Position);
						sprite.Play("unalert");
					}
					{
						foreach (SyncedPufferCollider component in Scene.Tracker.GetComponents<SyncedPufferCollider>()) {
							component.Check(this);
						}
						break;
					}
				}

				case States.Hit:
					lastSpeedPosition = Position;
					MoveH(hitSpeed.X * Engine.DeltaTime, onCollideH);
					MoveV(hitSpeed.Y * Engine.DeltaTime, onCollideV);
					anchorPosition = Position;
					hitSpeed.X = Calc.Approach(hitSpeed.X, 0f, 150f * Engine.DeltaTime);
					hitSpeed = Calc.Approach(hitSpeed, Vector2.Zero, StunnedAccel * Engine.DeltaTime);
					if (ProximityExplodeCheck()) {
						Explode();
						GotoGone();
						break;
					}
					if (Top >= (float)(SceneAs<Level>().Bounds.Bottom + 5)) {
						sprite.Play("hidden");
						GotoGone();
						break;
					}
					foreach (SyncedPufferCollider component2 in Scene.Tracker.GetComponents<SyncedPufferCollider>()) {
						component2.Check(this);
					}
					if (hitSpeed == Vector2.Zero) {
						ZeroRemainderX();
						ZeroRemainderY();
						GotoIdle();
					}
					break;

				case States.Gone: {
					float num = goneTimer;
					goneTimer -= Engine.DeltaTime;
					if (goneTimer <= RespawnMoveTime) {
						if (num > RespawnMoveTime && returnCurve.GetLengthParametric(8) > 8f) {
							Audio.Play("event:/new_content/game/10_farewell/puffer_return", Position);
						}
						Position = returnCurve.GetPoint(Ease.CubeInOut(Calc.ClampedMap(goneTimer, RespawnMoveTime, 0f)));
					}
					if (goneTimer <= 0f) {
						Visible = (Collidable = true);
						GotoIdle();
					}
					break;
				}
			}
		}

		public bool HitSpring(Spring spring) {
			switch (spring.Orientation) {
				default:
					if (hitSpeed.Y >= 0f) {
						GotoHitSpeed(224f * -Vector2.UnitY);
						MoveTowardsX(spring.CenterX, 4f);
						bounceWiggler.Start();
						Alert(restart: true, playSfx: false);
						return true;
					}
					return false;
				case Spring.Orientations.WallLeft:
					if (hitSpeed.X <= 60f) {
						facing.X = 1f;
						GotoHitSpeed(280f * Vector2.UnitX);
						MoveTowardsY(spring.CenterY, 4f);
						bounceWiggler.Start();
						Alert(restart: true, playSfx: false);
						return true;
					}
					return false;
				case Spring.Orientations.WallRight:
					if (hitSpeed.X >= -60f) {
						facing.X = -1f;
						GotoHitSpeed(280f * -Vector2.UnitX);
						MoveTowardsY(spring.CenterY, 4f);
						bounceWiggler.Start();
						Alert(restart: true, playSfx: false);
						return true;
					}
					return false;
			}
		}

		private bool ProximityExplodeCheck() {
			if (cantExplodeTimer > 0f) {
				return false;
			}
			bool result = false;
			Collider collider = Collider;
			Collider = detectRadius;
			Player player;
			if ((player = CollideFirst<Player>()) != null && player.CenterY >= Y + collider.Bottom - 4f && !Scene.CollideCheck<Solid>(Position, player.Center)) {
				result = true;
			}
			Collider = collider;
			return result;
		}

		private bool AlertedCheck() {
			Player entity = Scene.Tracker.GetEntity<Player>();
			if (entity != null) {
				return (entity.Center - Center).Length() < AlertedRadius;
			}
			return false;
		}

		private void Alert(bool restart, bool playSfx) {
			if (sprite.CurrentAnimationID == "idle") {
				if (playSfx) {
					Audio.Play("event:/new_content/game/10_farewell/puffer_expand", Position);
				}
				sprite.Play("alert");
				inflateWiggler.Start();
			}
			else if (restart && playSfx) {
				Audio.Play("event:/new_content/game/10_farewell/puffer_expand", Position);
			}
			alertTimer = 2f;
		}

		private void OnPlayer(Player player) {
			if (state == States.Gone || !(cantExplodeTimer <= 0f)) {
				return;
			}
			if (cannotHitTimer <= 0f) {
				if (player.Bottom > lastSpeedPosition.Y + 3f) {
					Explode();
					GotoGone();
				}
				else {
					player.Bounce(Top);
					GotoHit(player.Center);
					MoveToX(anchorPosition.X);
					idleSine?.Reset();
					anchorPosition = (lastSinePosition = Position);
					eyeSpin = 1f;
				}
			}
			cannotHitTimer = 0.1f;
		}

		public override void Added(Scene scene) {
			base.Added(scene);
			EntityStateTracker.AddListener(this, false);
		}

		public override void SceneEnd(Scene scene) {
			base.SceneEnd(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public override void Removed(Scene scene) {
			base.Removed(scene);
			EntityStateTracker.RemoveListener(this);
		}

		public static SyncBehavior GetSyncBehavior() => new SyncBehavior() {
			Header = 21,
			Parser = ParseState,
			StaticHandler = null,
			DiscardIfNoListener = false,
			DiscardDuplicates = false,
			Critical = false,
		};

		public EntityID GetID() => id;

		public bool CheckRecurringUpdate() => false;

		public static SyncedPufferState ParseState(CelesteNetBinaryReader r) {
			return new SyncedPufferState() {
				State = (States)Enum.Parse(typeof(States), r.ReadString()),
				Position = r.ReadVector2(),
			};
		}

		public void WriteState(CelesteNetBinaryWriter w) {
			w.Write(state.ToString() ?? "");
			w.Write(Position);
		}

		public void ApplyState(object state) {
			if (state is SyncedPufferState sps && sps.State != this.state
				&& !(this.state == States.Gone && goneTimer > RespawnTime/2f)) {
				applyingRemoteState = true;
				Position = sps.Position;
				if (sps.State == States.Hit) {
					Visible = true;
					Collidable = true;
					GotoHit(Position);
				}
				else if (sps.State == States.Gone) {
					Explode();
					GotoGone();
				}
				applyingRemoteState = false;
			}
		}

	}

	public class SyncedPufferState {
		public SyncedPuffer.States State;
		public Vector2 Position;
	}

	[Tracked]
	public class SyncedPufferCollider : Component {
		public Action<SyncedPuffer> OnCollide;
		public Collider Collider;

		public SyncedPufferCollider(Action<SyncedPuffer> onCollide, Collider collider = null)
			: base(active: false, visible: false) {
			OnCollide = onCollide;
			Collider = null;
		}

		public void Check(SyncedPuffer puffer) {
			if (OnCollide != null) {
				Collider collider = Entity.Collider;
				if (Collider != null) {
					Entity.Collider = Collider;
				}
				if (puffer.CollideCheck(Entity)) {
					OnCollide(puffer);
				}
				Entity.Collider = collider;
			}
		}
	}
}
