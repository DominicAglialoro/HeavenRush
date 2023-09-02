local rushLevelController = {}

rushLevelController.name = "heavenRush/rushLevelController"
rushLevelController.depth = -1000000
rushLevelController.texture = "loenn/heavenRush/rushLevelController"
rushLevelController.placements = {
	name = "controller",
	data = {
		levelName = "",
		levelNumber = "",
		requireKillAllDemons = true,
		berryObjectiveTime = 0
	}
}

return rushLevelController