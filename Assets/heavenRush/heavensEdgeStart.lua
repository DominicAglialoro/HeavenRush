function onBegin()
	disableMovement()
    player.ForceCameraUpdate = true
	runTo(-904)
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
	player.StateMachine.State = 20
	wait(1)
end

function onEnd(level, wasSkipped)
	if wasSkipped then
		playMusic("event:/heavenRush/music/machine_girl_cloud_nine")
		teleportTo(-904, -64, "c")
    end
	
	player.ForceCameraUpdate = false
	enableMovement()
end