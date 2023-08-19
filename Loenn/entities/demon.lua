local demon = {}

demon.name = "heavenRush/demon"
demon.depth = -100
demon.placements = {
	{
		name = "grounded",
		data = {
			grounded = true,
			hasCard = false,
			cardType = "Yellow"
		}
	},
	{
		name = "aerial",
		data = {
			grounded = false,
			hasCard = false,
			cardType = "Yellow"
		}
	}
}

demon.fieldInformation = {
	cardType = {
		options = {
			"Yellow",
			"Blue",
			"Green",
			"Red",
			"White"
		},
		editable = false
	}
}

function demon.texture(room, entity)
	return entity.grounded and "loenn/demon/demonGrounded" or "loenn/demon/demonAerial"
end

return demon