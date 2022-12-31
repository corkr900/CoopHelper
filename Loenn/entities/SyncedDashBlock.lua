local fakeTilesHelper = require("helpers.fake_tiles")

local syncedDashBlock = {}

syncedDashBlock.name = "corkr900CoopHelper/SyncedDashBlock"
syncedDashBlock.depth = 0
syncedDashBlock.placements = {
    name = "dash_block",
    data = {
        tiletype = "3",
        blendin = true,
        canDash = true,
        permanent = true,
        width = 8,
        height = 8
    }
}

syncedDashBlock.sprite = fakeTilesHelper.getEntitySpriteFunction("tiletype", "blendin")
syncedDashBlock.fieldInformation = fakeTilesHelper.getFieldInformation("tiletype")

return syncedDashBlock