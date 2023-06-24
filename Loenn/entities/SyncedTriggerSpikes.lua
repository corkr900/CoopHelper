local spikeHelper = require('helpers.spikes')

local spikeVariants = {
    "dust",
}

local spikeUp = spikeHelper.createEntityHandler("corkr900CoopHelper/TriggerSpikesUp", "up", false, true, spikeVariants)
local spikeDown = spikeHelper.createEntityHandler("corkr900CoopHelper/TriggerSpikesDown", "down", false, true, spikeVariants)
local spikeLeft = spikeHelper.createEntityHandler("corkr900CoopHelper/TriggerSpikesLeft", "left", false, true, spikeVariants)
local spikeRight = spikeHelper.createEntityHandler("corkr900CoopHelper/TriggerSpikesRight", "right", false, true, spikeVariants)

spikeUp.sprite = function(room, entity) 
    return spikeHelper.getTriggerSpikeSprites(entity, "up", true, "dust")
end
spikeDown.sprite = function(room, entity) 
    return spikeHelper.getTriggerSpikeSprites(entity, "down", true, "dust")
end
spikeLeft.sprite = function(room, entity) 
    return spikeHelper.getTriggerSpikeSprites(entity, "left", true, "dust")
end
spikeRight.sprite = function(room, entity) 
    return spikeHelper.getTriggerSpikeSprites(entity, "right", true, "dust")
end

return {spikeUp,spikeDown,spikeLeft,spikeRight}