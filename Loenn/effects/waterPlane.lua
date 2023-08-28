local waterPlane = { }

waterPlane.name = "heavenRush/waterPlane"
waterPlane.canBackground = true
waterPlane.canForeground = false

waterPlane.fieldInformation = {
	farScrollMult = {
		minimumValue = 0.000001
	}
}

waterPlane.defaultData = {
	texture = "",
	y = 0,
	scrollx = 0,
	scrolly = 0,
	speedx = 0,
	farScrollMult = 1,
	alpha = 1
}

return waterPlane