function onBegin()
	disableMovement()
    player.ForceCameraUpdate = true
	player.Dashes = 1
	runTo(768)
	wait(0.5)
	setFlag("c_fall_1", true)
	wait(0.2)
	setFlag("c_fall_2", true)
	wait(0.4)
	setFlag("c_fall_3", true)
	wait(0.3)
	setFlag("c_fall_4", true)
	wait(1)
	setFlag("c_break", true)
	wait(2)
	playMusic("event:/heavenRush/music/machine_girl_cloud_nine")
	waitUntilOnGround()
	setPlayerState(20)
	wait(1)
end

function onEnd(level, wasSkipped)
	player.ForceCameraUpdate = false
	enableMovement()
	
	if wasSkipped then
		playMusic("event:/heavenRush/music/machine_girl_cloud_nine")
		setDarkness(0.05)
		teleportTo(768, -72, "0_01_c")
    end
end