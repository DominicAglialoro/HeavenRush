using System.Collections.Generic;

namespace Celeste.Mod.HeavenRush; 

public class CardInventory {
    private const int MAX_COUNT = 3;

    public int CardCount => cards.Count;

    public IEnumerable<AbilityCardType> Cards => cards;

    private Queue<AbilityCardType> cards = new(MAX_COUNT);

    public void Reset() => cards.Clear();

    public AbilityCardType? PopCard() => cards.Count > 0 ? cards.Dequeue() : null;

    public AbilityCardType? PeekCard() => cards.Count > 0 ? cards.Peek() : null;

    public bool TryAddCard(AbilityCardType cardType) {
        if (cards.Count == MAX_COUNT)
            return false;
        
        cards.Enqueue(cardType);

        return true;
    }
}