local rushLevelController = {}

rushLevelController.name = "heavenRush/rushLevelController"
rushLevelController.depth = -1000000
rushLevelController.texture = "loenn/rushLevelController/rushLevelController"
rushLevelController.placements = {
	name = "controller",
	data = {
		levelName = "",
		requireKillAllDemons = true,
		berryObjectiveTime = 0
	}
}

return rushLevelController