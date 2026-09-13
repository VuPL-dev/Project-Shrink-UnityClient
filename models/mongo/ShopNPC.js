const mongoose = require('mongoose');

const ShopProductSchema = new mongoose.Schema({
  itemId: { type: String, required: true },
  price: { type: Number, required: true },
  currencyType: { type: String, default: 'Gold' },
  stockCount: { type: Number, default: -1 } // -1 nghĩa là vô hạn
});

const ShopNPCSchema = new mongoose.Schema({
  npcId: { type: String, required: true, unique: true },
  name: { type: String, required: true },
  location: { type: String }, // Ví dụ: 'SafeZone_01'
  dialogues: {
    greeting: { type: String, default: 'Welcome!' },
    farewell: { type: String, default: 'See you later.' }
  },
  products: [ShopProductSchema]
}, { timestamps: true });

module.exports = mongoose.model('ShopNPC', ShopNPCSchema);
