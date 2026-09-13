const { sequelize } = require('../../config/postgres');
const User = require('./User');
// Require other PG models here when created

const syncDatabase = async () => {
  try {
    // Tự động tạo bảng dựa trên Model (nếu chưa có)
    await sequelize.sync({ alter: true });
    console.log('✅ PostgreSQL tables synchronized successfully.');
  } catch (error) {
    console.error('❌ Error synchronizing PostgreSQL tables:', error);
  }
};

module.exports = {
  User,
  syncDatabase
};
