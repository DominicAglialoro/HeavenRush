local abilityCard = {}

abilityCard.name = "heavenRush/abilityCard"
abilityCard.depth = -100
abilityCard.placements = {
	{
		name = "yellow",
		data = {
			cardType = "Yellow"
		}
	},
	{
		name = "blue",
		data = {
			cardType = "Blue"
		}
	},
	{
		name = "green",
		data = {
			cardType = "Green"
		}
	},
	{
		name = "red",
		data = {
			cardType = "Red"
		}
	},
	{
		name = "white",
		data = {
			cardType = "White"
		}
	}
}

abilityCard.fieldInformation = {
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

function abilityCard.texture(room, entity)
	return "heavenRush/loenn/card" .. entity.cardType
end

return abilityCard