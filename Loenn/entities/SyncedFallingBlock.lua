local fakeTilesHelper = require("helpers.fake_tiles")

local syncedFallingBlock = {}

syncedFallingBlock.name = "corkr900CoopHelper/SyncedFallingBlock"
syncedFallingBlock.placements = {
    name = "default",
    data = {
        tiletype = "3",
        climbFall = true,
        behind = false,
        width = 8,
        height = 8
    }
}

syncedFallingBlock.sprite = fakeTilesHelper.getEntitySpriteFunction("tiletype", false)
syncedFallingBlock.fieldInformation = fakeTilesHelper.getFieldInformation("tiletype")

function syncedFallingBlock.depth(room, entity)
    return entity.behind and 5000 or 0
end

return syncedFallingBlock