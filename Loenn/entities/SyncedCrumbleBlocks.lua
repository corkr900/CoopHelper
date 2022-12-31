local drawableNinePatch = require("structs.drawable_nine_patch")
local utils = require("utils")

local syncedCrumbleBlock = {}

local textures = {
    "default", "cliffside"
}

syncedCrumbleBlock.name = "corkr900CoopHelper/SyncedCrumbleBlocks"
syncedCrumbleBlock.depth = 0
syncedCrumbleBlock.fieldInformation = {
    texture = {
        options = textures,
    }
}
syncedCrumbleBlock.placements = {}

for _, texture in ipairs(textures) do
    table.insert(syncedCrumbleBlock.placements, {
        name = texture,
        data = {
            width = 8,
            texture = texture,
            shakeTimeTop = 0.6,
            shakeTimeSide = 1.0,
            respawnDelay = 2.0,
            breakOnJump = true,
        }
    })
end

local ninePatchOptions = {
    mode = "fill",
    fillMode = "repeat",
    border = 0
}

function syncedCrumbleBlock.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width = math.max(entity.width or 0, 8)

    local variant = entity.texture or "default"
    local texture = "objects/crumbleBlock/" .. variant
    local ninePatch = drawableNinePatch.fromTexture(texture, ninePatchOptions, x, y, width, 8)

    return ninePatch
end

function syncedCrumbleBlock.selection(room, entity)
    return utils.rectangle(entity.x or 0, entity.y or 0, math.max(entity.width or 0, 8), 8)
end

return syncedCrumbleBlock