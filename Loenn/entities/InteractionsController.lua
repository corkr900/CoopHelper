local utils = require("utils")

local InteractionsController = {}
InteractionsController.name = "corkr900CoopHelper/ForceInteractionsController"
InteractionsController.depth = 0
InteractionsController.placements = {
    {
        name = "default",
        data = {
			forceSettingTo = false,
		 }
    }
}

InteractionsController.offset = { 16, 16 }
InteractionsController.texture = "corkr900/CoopHelper/InteractionsController/icon00"

function InteractionsController.selection(room, entity)
    return utils.rectangle(entity.x - 16, entity.y - 16, 32, 32)
end

return InteractionsController
