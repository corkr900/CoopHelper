local utils = require("utils")

local deathModeOptions = {
    None = "None",
    SameRoomOnly = "SameRoomOnly",
    Everywhere = "Everywhere",
}

local SessionPicker = {}
SessionPicker.name = "corkr900CoopHelper/SessionPicker"
SessionPicker.depth = 0
SessionPicker.fieldInformation = {
    deathSyncMode = {
        options = deathModeOptions,
        editable = false
    }
}
SessionPicker.placements = {
    {
        name = "default",
        data = {
            removeIfSessionExists = true,
            skins = "",
            dashes = "",
            abilities = "",
            deathSyncMode = "SameRoomOnly"
        }
    }
}

SessionPicker.offset = { 8, 16 }
SessionPicker.texture = "corkr900/CoopHelper/SessionPicker/idle00"

function SessionPicker.selection(room, entity)
    return utils.rectangle(entity.x - 8, entity.y - 16, 16, 32)
end

return SessionPicker
