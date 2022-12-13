module corkr900CoopHelperEntityPicker

using ..Ahorn, Maple

@mapdef Entity "corkr900/CoopHelper/EntityPicker" EntityPicker(x::Integer, y::Integer)

const placements = Ahorn.PlacementDict(
    "Entity Picker (Co-op Helper)" => Ahorn.EntityPlacement(
        EntityPicker
    )
)

sprite = "corkr900/CoopHelper/SessionPicker/Idle00.png"

function Ahorn.selection(entity::EntityPicker)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::EntityPicker, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end