const { DataTypes } = require('sequelize');
const { sequelize } = require('../../config/postgres');

const User = sequelize.define('User', {
  id: {
    type: DataTypes.UUID,
    defaultValue: DataTypes.UUIDV4,
    primaryKey: true,
  },
  username: {
    type: DataTypes.STRING,
    allowNull: false,
    unique: true,
  },
  password: {
    type: DataTypes.STRING,
    allowNull: false,
  },
  role: {
    type: DataTypes.ENUM('Player', 'Moderator', 'ShopManager', 'Admin'),
    defaultValue: 'Player',
  },
  email: {
    type: DataTypes.STRING,
    allowNull: true,
    unique: true,
  }
}, {
  timestamps: true,
  tableName: 'users'
});

module.exports = User;
