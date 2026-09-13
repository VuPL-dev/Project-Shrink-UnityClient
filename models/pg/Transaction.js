const { DataTypes } = require('sequelize');
const { sequelize } = require('../../config/postgres');
const User = require('./User');

const Transaction = sequelize.define('Transaction', {
  id: {
    type: DataTypes.UUID,
    defaultValue: DataTypes.UUIDV4,
    primaryKey: true,
  },
  userId: {
    type: DataTypes.UUID,
    allowNull: false,
    references: {
      model: User,
      key: 'id'
    }
  },
  amount: {
    type: DataTypes.DECIMAL(10, 2),
    allowNull: false,
  },
  currency: {
    type: DataTypes.STRING,
    defaultValue: 'USD',
  },
  status: {
    type: DataTypes.ENUM('Pending', 'Completed', 'Failed', 'Refunded'),
    defaultValue: 'Pending',
  },
  transactionType: {
    type: DataTypes.ENUM('TopUp', 'Purchase', 'Refund'),
    allowNull: false,
  }
}, {
  timestamps: true,
  tableName: 'transactions'
});

// Quan hệ
User.hasMany(Transaction, { foreignKey: 'userId' });
Transaction.belongsTo(User, { foreignKey: 'userId' });

module.exports = Transaction;
