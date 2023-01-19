local syncedDialogTrigger = {}

syncedDialogTrigger.name = "corkr900CoopHelper/SyncedDialogCutscene"
syncedDialogTrigger.fieldInformation = {
    deathCount = {
        fieldType = "integer",
    }
}
syncedDialogTrigger.placements = {
    name = "dialog",
    data = {
        endLevel = false,
        onlyOnce = true,
        dialogId = "",
        deathCount = -1,
        miniTextBox = false,
    }
}

return syncedDialogTrigger