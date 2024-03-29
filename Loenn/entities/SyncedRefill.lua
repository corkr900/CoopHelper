local refill = {}

refill.name = "corkr900CoopHelper/SyncedRefill"
refill.depth = -100
refill.placements = {
    {
        name = "one_dash",
        data = {
            oneUse = false,
            twoDash = false,
            respawnTime = 2.5,
            alwaysBreak = false,
        }
    },
    {
        name = "two_dashes",
        data = {
            oneUse = false,
            twoDash = true,
            respawnTime = 2.5,
            alwaysBreak = false,
        }
    }
}

function refill.texture(room, entity)
    return entity.twoDash and "objects/refillTwo/idle00" or "objects/refill/idle00"
end

return refill