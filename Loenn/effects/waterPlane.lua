local waterPlane = { }

waterPlane.name = "heavenRush/waterPlane"
waterPlane.canBackground = true
waterPlane.canForeground = false

waterPlane.fieldOrder = {
	"nearY",
	"farY",
	"nearScrollY",
	"farScrollY",
	"waveNearDensity",
	"waveFarDensity",
	"waveNearScroll",
	"waveFarScroll",
	"waveNearSpeed",
	"waveFarSpeed",
	"waveNearWidth",
	"waveFarWidth",
	"waveNearColor",
	"waveFarColor",
	"texture"
}

waterPlane.fieldInformation = {
	nearY = { fieldType = "integer" },
	farY = { fieldType = "integer" },
	waveNearDensity = { minimumValue = 0 },
	waveFarDensity = { minimumValue = 0 },
	waveNearWidth = { fieldType = "integer", minimumValue = 1 },
	waveFarWidth = { fieldType = "integer", minimumValue = 1 },
	waveNearColor = { fieldType = "color" },
	waveFarColor = { fieldType = "color" }
}

waterPlane.defaultData = {
	nearY = 0,
	farY = 0,
	nearScrollY = 0,
	farScrollY = 0,
	waveNearDensity = 0,
	waveFarDensity = 0,
	waveNearScroll = 0,
	waveFarScroll = 0,
	waveNearSpeed = 0,
	waveFarSpeed = 0,
	waveNearWidth = 1,
	waveFarWidth = 1,
	waveNearColor = "ffffff",
	waveFarColor = "ffffff",
	texture = ""
}

return waterPlane