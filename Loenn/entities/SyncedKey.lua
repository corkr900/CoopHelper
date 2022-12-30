local syncedKey = {}

syncedKey.name = "corkr900CoopHelper/SyncedKey"
syncedKey.depth = -1000000
syncedKey.nodeLineRenderType = "line"
syncedKey.texture = "collectables/key/idle00"

-- Node with return just needs to have two nodes
-- Placements will update their position correctly
-- This is required since there is no one node key, only zero or two
syncedKey.placements = {
    {
        name = "normal"
    },
    {
        name = "with_return",
        placementType = "point",
        data = {
            nodes = {
                {x = 0, y = 0},
                {x = 0, y = 0}
            }
        }
    }
}

function syncedKey.nodeLimits(room, entity)
    local nodes = entity.nodes or {}

    if #nodes > 0 then
        return 2, 2

    else
        return 0, 0
    end
end

return syncedKey