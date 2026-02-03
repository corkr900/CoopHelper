local drawableSprite = require("structs.drawable_sprite")

local theoCrystal = {}

theoCrystal.name = "corkr900CoopHelper/SyncedTheoCrystal"
theoCrystal.depth = 100
theoCrystal.placements = {
	{
    	name = "theo_crystal",
		data = {
			enforceBounds = false,
			deduplicate = false,
		}
	},
}

-- Offset is from sprites.xml, not justifications
local offsetY = -10
local texture = "characters/theoCrystal/idle00"

function theoCrystal.sprite(room, entity)
    local sprite = drawableSprite.fromTexture(texture, entity)

    sprite.y += offsetY

    return sprite
end

--return theoCrystal
return nil  -- Not implemented yet :/