local drawableSpriteStruct = require("structs.drawable_sprite")
local surfPlatform = {}

surfPlatform.name = "heavenRush/surfPlatform"
surfPlatform.depth = 0
surfPlatform.canResize = { true, false }
surfPlatform.placements = {
	name = "platform",
	data = {
		width = 8
	}
}

function surfPlatform.sprite(room, entity)
    local texture = "loenn/heavenRush/surfPlatform"
	
	local x, y = entity.x or 0, entity.y or 0
    local width = entity.width or 8

    local startX, startY = math.floor(x / 8) + 1, math.floor(y / 8) + 1
    local stopX = startX + math.floor(width / 8) - 1
    local len = stopX - startX

    local sprites = {}

    for i = 0, len do
        local sprite = drawableSpriteStruct.fromTexture(texture, entity)

        sprite:setJustification(0, 0)
        sprite:addPosition(i * 8, 0)

        table.insert(sprites, sprite)
    end
	
	return sprites
end

return surfPlatform