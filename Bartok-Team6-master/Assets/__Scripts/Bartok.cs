using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//This enum contains the different phases of a game turn
public enum TurnPhase
{
	idle, 
	pre, 
	waiting, 
	post, 
	gameOver
}

public class Bartok : MonoBehaviour {

	static public Bartok S;
	//This field is static to enforce that there is only 1 current player
	static public Player CURRENT_PLAYER;
	public TextAsset deckXML;
	public TextAsset layoutXML;
	public Vector3 layoutCenter = Vector3.zero;

	//The number of degrees to fan each card in a hand
	public float handFanDegrees = 10f;
	public int numStartingCards = 7;
	public float drawTimeStagger = 0.1f;

	public bool ___________________________;

	public Deck deck;
	public List<CardBartok> drawPile;
	public List<CardBartok> discardPile;

	public BartokLayout layout;
	public Transform layoutAnchor;
	public List<Player> players;
	public CardBartok targetCard;

	public TurnPhase phase = TurnPhase.idle;
	public GameObject turnLight;

	public GameObject GTGameOver;
	public GameObject GTRoundResult;

	void Awake() 
	{
		S = this;

		//Find the TurnLight by name
		turnLight = GameObject.Find ("TurnLight");
		GTGameOver = GameObject.Find("GTGameOver");
		GTRoundResult = GameObject.Find("GTRoundResult");
		GTGameOver.SetActive (false);
		GTRoundResult.SetActive (false);
	}

	void Start()
	{
		deck = GetComponent<Deck> ();  //Get the deck
		deck.InitDeck (deckXML.text);  //Pass DeckXML to it
		Deck.Shuffle (ref deck.cards);  //This shuffles the deck
		//The ref keyword passes a refrence to deck.cards, which allows deck.cards to be modified by Deck.Shuffle()

		layout = GetComponent<BartokLayout> ();  //Get the layout
		layout.ReadLayout (layoutXML.text);  //Pass LayoutXML to it

		drawPile = UpgradeCardsList (deck.cards);
		LayoutGame ();
	}

	//UpgradeCardsList casts the cards in 1CD to be CardBartoks
	//Of course, they were all along, but this lets Unity know it
	List<CardBartok> UpgradeCardsList(List<Card> lCD)
	{
		List<CardBartok> lCB = new List<CardBartok>();
		foreach(Card tCD in lCD)
		{
			lCB.Add (tCD as CardBartok);
		}
		return(lCB);
	}

	//Position all the cards in the drawPile properly
	public void ArrangeDrawPile()
	{
		CardBartok tCB;

		for (int i=0; i<drawPile.Count; i++)
		{
			tCB = drawPile[i];
			tCB.transform.parent = layoutAnchor;
			tCB.transform.localPosition = layout.drawPile.pos;
			//Rotation should start at 0
			tCB.faceUp = false;
			tCB.SetSortingLayerName(layout.drawPile.layerName);
			tCB.SetSortOrder(-i*4);  //Order thm front-to-back
			tCB.state = CBState.drawpile;
		}
	}

	//Perform the initial game layout
	void LayoutGame()
	{
		if (layoutAnchor == null)
		{
			GameObject tGO = new GameObject("_LayoutAnchor");
			//^Create an empty GameObject named _LayoutAnchor in the Heiarchy
			layoutAnchor = tGO.transform;  //Grab its transform
			layoutAnchor.transform.position = layoutCenter;  //Position it
		}

		//Position the drawPile cards
		ArrangeDrawPile ();

		//Setup the players
		Player pl;
		players = new List<Player> ();
		foreach (SlotDef tSD in layout.slotDefs)
		{
			pl = new Player();
			pl.handSlotDef = tSD;
			players.Add(pl);
			pl.playerNum = players.Count;
		}
		players [0].type = PlayerType.human;  //Make the 0th player human

		CardBartok tCB;
		//Deal 7 cards to each player
		for (int i=0; i<numStartingCards; i++)
		{
			for (int j=0; j<4; j++)
			{
				//There are always 4 players
				tCB = Draw ();  //Draw a card
				//Stagger the draw time a bit. Remember order of operations.
				tCB.timeStart = Time.time + drawTimeStagger * (i*4 + j);
				//^By setting the timeStart before calling AddCard, we
				//override the automatic setting of timeStart in
				//CardBartok.MoveTo().
				//Add the card to the player's hand.The moduls (%4)
				//results in a number from 0 to 3
				players[(j+1)%4].AddCard(tCB);
			}
		}

		//Call Bartok.DrawFirstTarget() when the hand cards have been drawn.
		Invoke("DrawFirstTarget", drawTimeStagger * (numStartingCards*4+4));
	}

	public void DrawFirstTarget()
	{
		//Flip up the first target card from the drawPile
		CardBartok tCB = MoveToTarget(Draw ());
		//Set the CardBartok to call CBCallback on this Bartok when it is done
		tCB.reportFinishTo = this.gameObject;
	}

	//This callback is used by the last card to be dealt at the begining
	//It is only used once per game.
	public void CBCallback(CardBartok cb)
	{
		//You sometimes want to have reporting of method calls like this  //1
		Utils.tr (Utils.RoundToPlaces (Time.time), "Bartok.CBCallback()",cb.name);

		StartGame ();  //Start the Game
	}

	public void StartGame()
	{
		//Pick the player to the left of the human to go first.
		//(players[0] is the human)
		PassTurn (1);
	}

	public void PassTurn(int num = -1)
	{
		if (num == -1)
		{
			int ndx = players.IndexOf(CURRENT_PLAYER);
			num = (ndx+1)%4;
		}
		int lastPlayerNum = -1;
		if (CURRENT_PLAYER != null)
		{
			lastPlayerNum = CURRENT_PLAYER.playerNum;
			//Check for Game Over and need to reshuffel discards
			if (CheckGameOver())
			{
				return;
			}
		}
		CURRENT_PLAYER = players [num];
		phase = TurnPhase.pre;

		CURRENT_PLAYER.TakeTurn();

		//Move the TurnLight to shine on the new CURRENT_PLAYER
		Vector3 lPos = CURRENT_PLAYER.handSlotDef.pos + Vector3.back*5;
		turnLight.transform.position = lPos;

		//Report the turn passing
		Utils.tr(Utils.RoundToPlaces(Time.time), "Bartok.PassTurn()", "Old: "+lastPlayerNum, "New: "+CURRENT_PLAYER.playerNum);
	}

	//ValidPlay varifies that the card chosen can be played on the discard pile
	public bool ValidPlay(CardBartok cb)
	{
		//It's a valid play if the rank is the same
		if (cb.rank == targetCard.rank) return(true);

		//It's a valid play if the suit is the same
		if (cb.suit == targetCard.suit)
		{
			return(true);
		}

		//Otherwise, return false
		return(false);
	}

	//This makes a new card the target
	public CardBartok MoveToTarget(CardBartok tCB)
	{
		tCB.timeStart = 0;
		tCB.MoveTo (layout.discardPile.pos + Vector3.back);
		tCB.state = CBState.toTarget;
		tCB.faceUp = true;
		tCB.SetSortingLayerName("10");  //layout.target.layerName);
		tCB.eventualSortLayer = layout.target.layerName;
		if (targetCard != null)
		{
			MoveToDiscard(targetCard);
		}

		targetCard = tCB;

		return(tCB);
	}

	public CardBartok MoveToDiscard(CardBartok tCB)
	{
		tCB.state = CBState.discard;
		discardPile.Add (tCB);
		tCB.SetSortingLayerName (layout.discardPile.layerName);
		tCB.SetSortOrder (discardPile.Count * 4);
		tCB.transform.localPosition = layout.discardPile.pos + Vector3.back / 2;

		return (tCB);
	}

	//The Draw function will pull a single card from the drawPile and return it
	public CardBartok Draw()
	{
		CardBartok cd = drawPile [0];  //Pull the 0th CardProspector
		drawPile.RemoveAt (0);  //Then remove it from List<> drawPile
		return(cd);  //And return it
	}

	/* Now is a good time to comment out this testing code  //2
	//This update method is used to test adding cards to players' hands
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			players[0].AddCard(Draw ());
		}
		if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			players[1].AddCard(Draw());
		}
		if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			players[2].AddCard(Draw());
		}
		if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			players[3].AddCard(Draw());
		}
	}
	*/

	public void CardClicked(CardBartok tCB)
	{
		//If it's not the humans turn, don't repsond
		if (CURRENT_PLAYER.type != PlayerType.human) return;
		//If the game is waiting on a card to move, don't respond
		if (phase == TurnPhase.waiting) return;

		//Act differently based on whether it was a card in hand or on the drawPile that was clicked
		switch (tCB.state)
		{
		case CBState.drawpile:
			//Draw the top card, not necessarily th one clicked.
			CardBartok cb = CURRENT_PLAYER.AddCard(Draw());
			cb.callbackPlayer = CURRENT_PLAYER;
			Utils.tr (Utils.RoundToPlaces(Time.time), "Bartok.CardClicked()", "Draw", cb.name);
			phase = TurnPhase.waiting;
			break;
		case CBState.hand:
			//Check to see whether the card is valid
			if (ValidPlay(tCB))
			{
				CURRENT_PLAYER.RemoveCard(tCB);
				MoveToTarget(tCB);
				tCB.callbackPlayer = CURRENT_PLAYER;
				Utils.tr(Utils.RoundToPlaces(Time.time), "Bartok.CardClicked()", "Play", tCB.name, targetCard.name+" is target");
				phase = TurnPhase.waiting;
			} else {
				//Just ignore it
				Utils.tr(Utils.RoundToPlaces(Time.time), "Bartok.CardClicked()", "Attempted to play", tCB.name, targetCard.name+" is target");
			}
			break;
		}
	}

	public bool CheckGameOver()
	{
		//See if we need to reshuffle the discard pile into the draw pile
		if (drawPile.Count == 0)
		{
			List<Card> cards = new List<Card>();
			foreach (CardBartok cb in discardPile)
			{
				cards.Add (cb);
			}
			discardPile.Clear();
			Deck.Shuffle(ref cards);
			drawPile = UpgradeCardsList(cards);
			ArrangeDrawPile();
		}

		//Check to see if the current player has won
		if (CURRENT_PLAYER.hand.Count == 0)
		{
			//The current player has won!
			if (CURRENT_PLAYER.type == PlayerType.human)
			{
				GTGameOver.guiText.text = "You Won!";
				GTRoundResult.guiText.text = "";
			} else {
				GTGameOver.guiText.text = "Game Over";
				GTRoundResult.guiText.text = "Player "+CURRENT_PLAYER.playerNum+ " won";
			}
			GTGameOver.SetActive(true);
			GTRoundResult.SetActive(true);
			phase = TurnPhase.gameOver;
			Invoke("RestartGame", 1);
			return(true);
		}

		return (false);
	}

	public void RestartGame()
	{
		CURRENT_PLAYER = null;
		Application.LoadLevel("__Bartok_Scene_0");
	}

}
