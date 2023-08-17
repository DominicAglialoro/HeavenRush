local abilityCard = {}

abilityCard.name = "HeavenRush/AbilityCard"
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
	return "Loenn/AbilityCard/Card" .. entity.cardType
end

return abilityCard