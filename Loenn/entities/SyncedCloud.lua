local drawableSpriteStruct = require("structs.drawable_sprite")

local syncedCloud = {}

syncedCloud.name = "corkr900CoopHelper/SyncedCloud"
syncedCloud.depth = 0
syncedCloud.placements = {
    {
        name = "normal",
        data = {
            fragile = false,
            small = false
        }
    },
    {
        name = "fragile",
        data = {
            fragile = true,
            small = false
        }
    }
}

local normalScale = 1.0
local smallScale = 29 / 35

local function getTexture(entity)
    local fragile = entity.fragile

    if fragile then
        return "objects/clouds/fragile00"

    else
        return "objects/clouds/cloud00"
    end
end

function syncedCloud.sprite(room, entity)
    local texture = getTexture(entity)
    local sprite = drawableSpriteStruct.fromTexture(texture, entity)
    local small = entity.small
    local scale = small and smallScale or normalScale

    sprite:setScale(scale, 1.0)

    return sprite
end

return syncedCloud