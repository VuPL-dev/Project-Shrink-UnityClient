const mongoose = require('mongoose');
require('dotenv').config();

const connectMongo = async () => {
  try {
    const mongoURI = `mongodb://${process.env.MONGO_USER}:${process.env.MONGO_PASSWORD}@${process.env.MONGO_HOST}:${process.env.MONGO_PORT}/${process.env.MONGO_DB}?authSource=admin`;
    await mongoose.connect(mongoURI, {
      useNewUrlParser: true,
      useUnifiedTopology: true,
    });
    console.log('✅ MongoDB connected successfully.');
  } catch (error) {
    console.error('❌ Unable to connect to the MongoDB database:', error);
  }
};

module.exports = connectMongo;
