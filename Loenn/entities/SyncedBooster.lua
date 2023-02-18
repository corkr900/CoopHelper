local syncedBooster = {}

syncedBooster.name = "corkr900CoopHelper/SyncedBooster"
syncedBooster.depth = -8500
syncedBooster.placements = {
    {
        name = "green",
        data = {
            red = false
        }
    },
    {
        name = "red",
        data = {
            red = true
        }
    }
}

function syncedBooster.texture(room, entity)
    local red = entity.red

    if red then
        return "objects/booster/boosterRed00"

    else
        return "objects/booster/booster00"
    end
end

return syncedBooster
