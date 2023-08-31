local waterPlane = { }

waterPlane.name = "heavenRush/waterPlane"
waterPlane.canBackground = true
waterPlane.canForeground = false

waterPlane.defaultData = {
	texture = "",
	y = 0,
	scrollx = 0,
	scrolly = 0,
	speedx = 0,
	nearY = 0,
	nearScrollMult = 1,
	alpha = 1,
	pointCount = 80,
	farPointAlpha = 0.01,
	nearPointAlpha = 0.01
}

return waterPlane