local syncedSummitBackgroundManager = {}

syncedSummitBackgroundManager.name = "corkr900CoopHelper/SyncedSummitBackgroundManager"
syncedSummitBackgroundManager.depth = 0
syncedSummitBackgroundManager.texture = "@Internal@/summit_background_manager"
syncedSummitBackgroundManager.fieldInformation = {
    index = {
        fieldType = "integer",
    }
}
syncedSummitBackgroundManager.placements = {
    name = "manager",
    data = {
        cutscene = "",
        dark = false,
        ambience = "",
    }
}

return syncedSummitBackgroundManager