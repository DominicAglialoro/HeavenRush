local xnaColors = require("consts.xna_colors")
local lightBlue = xnaColors.LightBlue

local surfPlatform = {}

surfPlatform.name = "heavenRush/surfPlatform"
surfPlatform.fillColor = {lightBlue[1] * 0.3, lightBlue[2] * 0.3, lightBlue[3] * 0.3, 0.6}
surfPlatform.borderColor = {lightBlue[1] * 0.8, lightBlue[2] * 0.8, lightBlue[3] * 0.8, 0.8}
surfPlatform.placements = {
	name = "platform",
	data = {
		width = 8,
		height = 8
	}
}

surfPlatform.depth = 0

return surfPlatform