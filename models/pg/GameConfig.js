const { DataTypes } = require('sequelize');
const { sequelize } = require('../../config/postgres');

const GameConfig = sequelize.define('GameConfig', {
  id: {
    type: DataTypes.UUID,
    defaultValue: DataTypes.UUIDV4,
    primaryKey: true,
  },
  configKey: {
    type: DataTypes.STRING,
    allowNull: false,
    unique: true,
  },
  configValue: {
    type: DataTypes.JSONB,
    allowNull: false,
  },
  description: {
    type: DataTypes.STRING,
    allowNull: true,
  },
  updatedBy: {
    type: DataTypes.UUID,
    allowNull: true, // ID của Admin hoặc Moderator
  }
}, {
  timestamps: true,
  tableName: 'game_configs'
});

module.exports = GameConfig;
