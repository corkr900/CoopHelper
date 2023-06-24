local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")

local syncedFeather = {}

syncedFeather.name = "corkr900CoopHelper/SyncedFeather"
syncedFeather.depth = 0
syncedFeather.placements = {
    name = "normal",
    data = {
        shielded = false,
        singleUse = false,
    }
}

function syncedFeather.draw(room, entity, viewport)
    local featherSprite = drawableSprite.fromTexture("objects/flyFeather/idle00", entity)
    local shielded = entity.shielded or false

    if shielded then
        local x, y = entity.x or 0, entity.y or 0

        love.graphics.circle("line", x, y, 12)
    end

    featherSprite:draw()
end

function syncedFeather.selection(room, entity)
    if entity.shielded then
        return utils.rectangle(entity.x - 12, entity.y - 12, 24, 24)

    else
        local sprite = drawableSprite.fromTexture("objects/flyFeather/idle00", entity)

        return sprite:getRectangle()
    end
end

return syncedFeather