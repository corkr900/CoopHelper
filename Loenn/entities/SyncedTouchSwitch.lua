local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")

local syncedTouchSwitch = {}

syncedTouchSwitch.name = "corkr900CoopHelper/SyncedTouchSwitch"
syncedTouchSwitch.depth = 2000
syncedTouchSwitch.placements = {
    {
        name = "default",
		data = {

		}
    }
}

local containerTexture = "objects/touchswitch/container"
local iconTexture = "objects/touchswitch/icon00"

function syncedTouchSwitch.sprite(room, entity)
    local containerSprite = drawableSprite.fromTexture(containerTexture, entity)
    local iconSprite = drawableSprite.fromTexture(iconTexture, entity)

    return {containerSprite, iconSprite}
end

return syncedTouchSwitch