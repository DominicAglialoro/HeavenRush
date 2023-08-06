namespace Celeste.Mod.HeavenRush; 

public class CardInventory {
    private const int MAX_COUNT = 3;
    
    public AbilityCardType CardType { get; private set; }
    
    public int CardCount { get; private set; }

    public void Reset() {
        CardType = AbilityCardType.Yellow;
        CardCount = 0;
    }

    public void PopCard() {
        if (CardCount > 0)
            CardCount--;
    }

    public bool TryAddCard(AbilityCardType cardType) {
        if (cardType == CardType) {
            if (CardCount >= MAX_COUNT)
                return false;

            CardCount++;

            return true;
        }

        CardType = cardType;
        CardCount = 1;

        return true;
    }
}