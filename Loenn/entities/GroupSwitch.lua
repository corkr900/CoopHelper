local utils = require("utils")

local GroupSwitch = {}
GroupSwitch.name = "corkr900CoopHelper/GroupButton"
GroupSwitch.depth = 0
GroupSwitch.placements = {
    {
        name = "default",
        data = { 
            flag = "Group_switch_flag_",
        }
    }
}

GroupSwitch.offset = { 16, 8 }
GroupSwitch.texture = "corkr900/CoopHelper/GroupSwitch/button00"

function GroupSwitch.selection(room, entity)
    return utils.rectangle(entity.x - 16, entity.y - 8, 32, 8)
end

return GroupSwitch
