local utils = require("utils")
local drawableSprite = require("structs.drawable_sprite")

local syncedBreakerBox = {}

syncedBreakerBox.name = "corkr900CoopHelper/SyncedLightningBreakerBox"
syncedBreakerBox.depth = -10550
syncedBreakerBox.texture = "objects/breakerBox/Idle00"
syncedBreakerBox.fieldInformation = {
    music_progress = {
        fieldType = "integer",
    }
}
syncedBreakerBox.placements = {
    name = "breaker_box",
    data = {
        flipX = false,
        music_progress = -1,
        music_session = false,
        music = "",
        flag = false
    }
}

function syncedBreakerBox.scale(room, entity)
    local scaleX = entity.flipX and -1 or 1

    return scaleX, 1
end

function syncedBreakerBox.justification(room, entity)
    local flipX = entity.flipX

    return flipX and 0.75 or 0.25, 0.25
end

return syncedBreakerBox