local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")

local syncedLockBlock = {}

local textures = {
    wood = "objects/door/lockdoor00",
    temple_a = "objects/door/lockdoorTempleA00",
    temple_b = "objects/door/lockdoorTempleB00",
    moon = "objects/door/moonDoor11"
}
local textureOptions = {}

for name, _ in pairs(textures) do
    textureOptions[utils.humanizeVariableName(name)] = name
end

syncedLockBlock.name = "corkr900CoopHelper/SyncedLockBlock"
syncedLockBlock.depth = 0
syncedLockBlock.justification = {0.25, 0.25}
syncedLockBlock.fieldInformation = {
    sprite = {
        options = textureOptions,
        editable = false
    }
}
syncedLockBlock.placements = {}

for name, texture in pairs(textures) do
    table.insert(syncedLockBlock.placements, {
        name = name,
        data = {
            sprite = name,
            unlock_sfx = "",
            stepMusicProgress = false
        }
    })
end

function syncedLockBlock.sprite(room, entity)
    local spriteName = entity.sprite or "wood"
    local texture = textures[spriteName] or textures["wood"]
    local sprite = drawableSprite.fromTexture(texture, entity)

    sprite:addPosition(16, 16)

    return sprite
end

return syncedLockBlock