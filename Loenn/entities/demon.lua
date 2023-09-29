local demon = {}

demon.name = "heavenRush/demon"
demon.depth = -100
demon.placements = {
	{
		name = "grounded",
		data = {
			grounded = true,
			dashRestores = 1
		}
	},
	{
		name = "aerial",
		data = {
			grounded = false,
			dashRestores = 1
		}
	}
}

demon.fieldInformation = {
	dashRestores = {
		fieldType = "integer",
		minimumValue = 0
	}
}

function demon.texture(room, entity)
	return entity.grounded and "loenn/heavenRush/demonGrounded" or "loenn/heavenRush/demonAerial"
end

function demon.color(room, entity)
	return entity.dashRestores == 0 and { 0, 1, 1 } or entity.dashRestores == 1 and { 1, 1, 1 } or entity.dashRestores == 2 and { 1, 0.4, 0.8 } or { 1, 0, 1 }
end

return demon