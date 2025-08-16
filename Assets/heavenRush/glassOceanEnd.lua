function onBegin()
	getLevel():RegisterAreaComplete()
	disableMovement()
	disableRetry()
    player.ForceCameraUpdate = true
	waitUntilOnGround()
	walkTo(33104)
	
	while true do
		wait(60)
	end
end

function onEnd(room, wasSkipped)
	if wasSkipped then
		completeArea()
	end
end