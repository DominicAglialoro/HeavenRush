function onBegin()
	playMusic("")
    disableMovement()
    player.ForceCameraUpdate = true
    wait(2.8)
	playMusic("event:/heavenRush/music/machine_girl_virtual_paradise")
end

function onEnd(level, wasSkipped)
    if wasSkipped then
		playMusic("event:/heavenRush/music/machine_girl_virtual_paradise")
		teleportTo(112, -64, "0_01_p")
    end
	
	player.ForceCameraUpdate = false
	enableMovement()
end