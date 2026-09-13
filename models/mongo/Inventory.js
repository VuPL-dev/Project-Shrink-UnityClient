const mongoose = require('mongoose');

const InventoryItemSchema = new mongoose.Schema({
  itemId: { type: String, required: true },
  quantity: { type: Number, default: 1 },
  slotIndex: { type: Number, required: true } // Vị trí trong túi đồ
});

const InventorySchema = new mongoose.Schema({
  userId: {
    type: String, // Maps to Postgres User UUID
    required: true,
    unique: true
  },
  capacity: {
    type: Number,
    default: 20
  },
  items: [InventoryItemSchema],
  quickSlots: {
    slot1: { type: String, default: null },
    slot2: { type: String, default: null },
    slot3: { type: String, default: null },
    slot4: { type: String, default: null }
  }
}, { timestamps: true });

module.exports = mongoose.model('Inventory', InventorySchema);
