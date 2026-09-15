const mongoose = require('mongoose');

const SaveStateSchema = new mongoose.Schema({
  userId: {
    type: String, // Maps to Postgres User UUID
    required: true,
    unique: true
  },
  lastCheckpoint: {
    type: String,
    default: 'Lab_Start'
  },
  playerStats: {
    hp: { type: Number, default: 100 },
    stamina: { type: Number, default: 100 },
    statusEffects: [String]
  },
  coordinates: {
    x: { type: Number, default: 0 },
    y: { type: Number, default: 0 },
    z: { type: Number, default: 0 }
  },
  questProgress: {
    type: Map,
    of: String // e.g., 'MainQuest_1': 'Completed'
  },
  environmentState: {
    type: Map,
    of: Boolean // e.g., 'Door_Lab_01_Opened': true
  }
}, { timestamps: true });

module.exports = mongoose.model('SaveState', SaveStateSchema);
