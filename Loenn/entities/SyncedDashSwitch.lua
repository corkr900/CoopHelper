local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")

local textures = {
    default = "objects/temple/dashButton00",
    mirror = "objects/temple/dashButtonMirror00",
}
local textureOptions = {
    "default",
    "mirror",
}
local sideOptions = {
    "Left",
    "Right",
    "Up",
    "Down",
}

local syncedDashSwitch = {}

syncedDashSwitch.name = "corkr900CoopHelper/SyncedDashSwitch"
syncedDashSwitch.depth = 0
syncedDashSwitch.justification = {0.5, 0.5}
syncedDashSwitch.fieldInformation = {
    sprite = {
        options = textureOptions,
    },
    side = {
        options = sideOptions,
        editable = false,
    },
}
syncedDashSwitch.placements = {
    {
        name = "default",
        data = {
            side = "Left",
            sprite = "default",
            persistent = false,
            allGates = false,
        }
    }
}

function syncedDashSwitch.sprite(room, entity)
    local leftSide = entity.leftSide
    local texture = entity.sprite == "default" and textures["default"] or textures["mirror"]
    local sprite = drawableSprite.fromTexture(texture, entity)

    if entity.side == "Left" then
        sprite:addPosition(0, 8)
        sprite.rotation = math.pi
    elseif entity.side == "Right" then
        sprite:addPosition(8, 8)
        sprite.rotation = 0
    elseif entity.side == "Up" then
        sprite:addPosition(8, 0)
        sprite.rotation = -math.pi / 2
    else
        sprite:addPosition(8, 8)
        sprite.rotation = math.pi / 2
    end

    return sprite
end

return syncedDashSwitch