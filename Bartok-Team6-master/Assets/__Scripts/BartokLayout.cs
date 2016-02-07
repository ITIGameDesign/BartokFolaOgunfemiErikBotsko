using UnityEngine;
using System.Collections;
using System.Collections.Generic;
// SlotDef class is not based on MonoBehaviour, so it doesn't need its own file.
[System.Serializable] // Makes SlotDef able to be seen in the Unity Inspector

//AAAAAAAAAAAAAAAAAAAAAAAAAAAAA!!!!!
// Fola's note: comment out the slotdef defining section from the original layout script from prospector in order to make this work

public class SlotDef {
	public float x;
	public float y;
	public bool faceUp=false;
	public string layerName="Default";
	public int layerID = 0;
	public int id;
	public List<int> hiddenBy = new List<int>(); // Unused in Bartok
	public float rot; // rotation of hands
	public string type="slot";
	public Vector2 stagger;
	public int player; // player number of a hand
	public Vector3 pos; // pos derived from x, y, & multiplier
}
public class BartokLayout : MonoBehaviour {
	public PT_XMLReader xmlr; // Just like Deck, this has an PT_XMLReader
	public PT_XMLHashtable xml; // This variable is for faster xml access
	public Vector2 multiplier; // Sets the spacing of the tableau
	// SlotDef references
	public List<SlotDef> slotDefs; // The SlotDefs hands
	public SlotDef drawPile;
	public SlotDef discardPile;
	public SlotDef target;

	// This function is called to read in the LayoutXML.xml file
	public void ReadLayout(string xmlText) {
		xmlr = new PT_XMLReader();
		xmlr.Parse(xmlText); // The XML is parsed
		xml = xmlr.xml["xml"][0]; // And xml is set as a shortcut to the XML

		// Read in the multiplier, which sets card spacing
		multiplier.x = float.Parse(xml["multiplier"][0].att("x"));
		multiplier.y = float.Parse(xml["multiplier"][0].att("y"));

		// Read in the slots
		SlotDef tSD;
		// slotsX is used as a shortcut to all the <slot>s
		PT_XMLHashList slotsX = xml["slot"];

		for (int i=0; i<slotsX.Count; i++) {
			tSD = new SlotDef(); // Create a new SlotDef instance
			if (slotsX[i].HasAtt("type")) {
				// If this <slot> has a type attribute parse it
				tSD.type = slotsX[i].att("type");
			} else {
				// If not, set its type to "slot"
				tSD.type = "slot";
			}
			// Various attributes are parsed into numerical values
			tSD.x = float.Parse( slotsX[i].att("x") );
			tSD.y = float.Parse( slotsX[i].att("y") );
			tSD.pos = new Vector3( tSD.x*multiplier.x, tSD.y*multiplier.y, 0 );

			// Sorting Layers
			tSD.layerID = int.Parse( slotsX[i].att("layer") );
			// In this game, the Sorting Layers are named 1, 2, 3, ...through 10
			// This converts the number of the layerID into a text layerName
			tSD.layerName = tSD.layerID.ToString();
			// The layers are used to make sure that the correct cards are
			// on top of the others. In Unity 2D, all of our assets are
			// effectively at the same Z depth, so sorting layers are used
			// to differentiate between them.

			// pull additional attributes based on the type of each <slot>
			switch (tSD.type) {
			case "slot":
				// ignore slots that are just of the "slot" type
				break;
			
			case "drawpile":
				// The drawPile xstagger is read but not actually used in Bartok
				tSD.stagger.x = float.Parse( slotsX[i].att("xstagger") );
				drawPile = tSD;
				break;
			
			case "discardpile":
				discardPile = tSD;
				break;
			
			case "target":
				// The target card has a different layer from discardPile
				target = tSD;
				break;
			
			case "hand":
				// Information for each player's hand
				tSD.player = int.Parse( slotsX[i].att("player") );
				tSD.rot = float.Parse( slotsX[i].att("rot") );
				slotDefs.Add (tSD);
				break;
			}
		}
	}
}
