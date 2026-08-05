function onBegin()
	getLevel():RegisterAreaComplete()
	disableMovement()
	disableRetry()
    player.ForceCameraUpdate = true
	waitUntilOnGround()
	walkTo(34672)
end

function onEnd(level, wasSkipped)
    if wasSkipped then
		completeArea()
	end
end