local cloudSea = { }

cloudSea.name = "heavenRush/cloudSea"
cloudSea.canBackground = true
cloudSea.canForeground = false

cloudSea.fieldOrder = {
	"nearY",
	"farY",
	"nearScrollY",
	"farScrollY",
	"layerCount",
	"layerHeight",
	"layerTopColor",
	"layerBottomColor",
	"layerOutlineColor",
	"waveCount",
	"waveNearScroll",
	"waveFarScroll",
	"waveNearScale",
	"waveFarScale",
	"waveMinAmplitude",
	"waveMaxAmplitude",
	"waveMinFrequency",
	"waveMaxFrequency",
	"waveMinSpeed",
	"waveMaxSpeed"
}

cloudSea.fieldInformation = {
	nearY = { fieldType = "integer" },
	farY = { fieldType = "integer" },
	layerCount = { fieldType = "integer" },
	layerHeight = { fieldType = "integer" },
	layerTopColor = { fieldType = "color" },
	layerBottomColor = { fieldType = "color" },
	layerOutlineColor = { fieldType = "color" },
	waveCount = { fieldType = "integer" }
}

cloudSea.defaultData = {
	nearY = 0,
	farY = 0,
	nearScrollY = 0,
	farScrollY = 0,
	layerCount = 1,
	layerHeight = 16,
	layerTopColor = "ffffff",
	layerBottomColor = "ffffff",
	layerOutlineColor = "ffffff",
	waveCount = 1,
	waveNearScroll = 0,
	waveFarScroll = 0,
	waveNearScale = 1,
	waveFarScale = 1,
	waveMinAmplitude = 0,
	waveMaxAmplitude = 0,
	waveMinFrequency = 0,
	waveMaxFrequency = 0,
	waveMinSpeed = 0,
	waveMaxSpeed = 0
}

return cloudSea