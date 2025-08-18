function onBegin()
	getLevel():RegisterAreaComplete()
	disableMovement()
	disableRetry()
    player.ForceCameraUpdate = true
	waitUntilOnGround()
	walkTo(21808)
end

function onEnd(level, wasSkipped)
    if wasSkipped then
		completeArea()
	end
end