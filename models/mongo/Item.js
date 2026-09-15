const mongoose = require('mongoose');

const ItemSchema = new mongoose.Schema({
  itemId: { type: String, required: true, unique: true },
  name: { type: String, required: true },
  description: { type: String },
  type: { 
    type: String, 
    enum: ['Material', 'Consumable', 'Weapon', 'QuestItem', 'KeyItem'],
    required: true 
  },
  maxStack: { type: Number, default: 99 },
  craftingRecipe: [{
    materialItemId: { type: String },
    quantityRequired: { type: Number }
  }],
  attributes: {
    hpRestore: { type: Number, default: 0 },
    staminaRestore: { type: Number, default: 0 },
    damage: { type: Number, default: 0 }
  }
}, { timestamps: true });

module.exports = mongoose.model('Item', ItemSchema);
