local drawableNinePatch = require("structs.drawable_nine_patch")

local syncedTempleCrackedBlock = {}

syncedTempleCrackedBlock.name = "corkr900CoopHelper/SyncedTempleCrackedBlock"
syncedTempleCrackedBlock.depth = 0
syncedTempleCrackedBlock.minimumSize = {24, 24}
syncedTempleCrackedBlock.placements = {
    name = "temple_block",
    data = {
        width = 24,
        height = 24,
        persistent = false
    }
}

local ninePatchOptions = {
    mode = "fill",
    borderMode = "repeat",
    fillMode = "repeat"
}

local blockTexture = "objects/temple/breakBlock00"

function syncedTempleCrackedBlock.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 24, entity.height or 24

    local ninePatch = drawableNinePatch.fromTexture(blockTexture, ninePatchOptions, x, y, width, height)

    return ninePatch
end

return syncedTempleCrackedBlock