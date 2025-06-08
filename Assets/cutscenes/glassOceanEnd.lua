function onBegin()
	disableMovement()
    player.ForceCameraUpdate = true
	disableRetry()
	waitUntilOnGround()
	walkTo(33104)
end

function onEnd(room, wasSkipped)
	if wasSkipped then
		completeArea ()
	end
end