local syncedPuffer = {}

syncedPuffer.name = "corkr900CoopHelper/SyncedPuffer"
syncedPuffer.depth = 0
syncedPuffer.texture = "objects/puffer/idle00"
syncedPuffer.placements = {
    {
        name = "normal",
        data = {
            right = false,
            static = false,
            sprite = "",
        }
    },
    {
        name = "static",
        data = {
            right = false,
            static = true,
            sprite = "",
        }
    }
}

function syncedPuffer.scale(room, entity)
    local right = entity.right

    return right and 1 or -1, 1
end

function syncedPuffer.flip(room, entity, horizontal, vertical)
    if horizontal then
        entity.right = not entity.right
    end

    return horizontal
end

return syncedPuffer