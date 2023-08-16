local textures = {
	"Loenn/AbilityCard/CardYellow",
	"Loenn/AbilityCard/CardBlue",
	"Loenn/AbilityCard/CardGreen",
	"Loenn/AbilityCard/CardRed",
	"Loenn/AbilityCard/CardWhite"
}

local abilityCard = {}

abilityCard.name = "HeavenRush/AbilityCard"
abilityCard.depth = -100
abilityCard.placements = {
	{
		name = "yellow",
		data = {
			cardType = 0
		}
	},
	{
		name = "blue",
		data = {
			cardType = 1
		}
	},
	{
		name = "green",
		data = {
			cardType = 2
		}
	},
	{
		name = "red",
		data = {
			cardType = 3
		}
	},
	{
		name = "white",
		data = {
			cardType = 4
		}
	}
}

function abilityCard.texture(room, entity)
	return textures[entity.cardType + 1]
end

return abilityCard