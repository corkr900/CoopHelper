local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")

local sessionGate = {}

local textures = {
    default = "objects/door/TempleDoor00",
    mirror = "objects/door/TempleDoorB00",
    theo = "objects/door/TempleDoorC00"
}

local textureOptions = {}

for texture, _ in pairs(textures) do
    textureOptions[utils.titleCase(texture)] = texture
end

sessionGate.name = "corkr900CoopHelper/SessionGate"
sessionGate.depth = -9000
sessionGate.canResize = {false, false}
sessionGate.fieldInformation = {
    sprite = {
        options = textureOptions,
        editable = true
    },
    requiredRole = {
        editable = true
    }
}
sessionGate.placements = {
    {
        name = "default",
        data = {
            sprite = "default",
            requiredRole = ""
        }
    }
}

function sessionGate.sprite(room, entity)
    local variant = entity.sprite or "default"
    local texture = textures[variant] or textures["default"]
    local sprite = drawableSprite.fromTexture(texture, entity)
    local height = entity.height or 48

    sprite:setJustification(0.5, 0.0)
    sprite:addPosition(4, height - 48)

    return sprite
end

return sessionGate
