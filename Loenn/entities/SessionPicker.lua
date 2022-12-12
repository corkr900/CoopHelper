local utils = require("utils")

local SessionPicker = {}
SessionPicker.name = "corkr900/CoopHelper/SessionPicker"
SessionPicker.depth = 0
SessionPicker.placements = {
    {
        name = "default",
        data = {
        }
    }
}

SessionPicker.offset = { 8, 16 }
SessionPicker.texture = "corkr900/CoopHelper/SessionPicker/idle00"

function SessionPicker.selection(room, entity)
    return utils.rectangle(entity.x - 8, entity.y - 16, 16, 32)
end

return SessionPicker
