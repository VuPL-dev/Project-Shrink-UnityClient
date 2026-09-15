const { User } = require('../models/pg');

const getAllUsers = async (req, res, next) => {
  try {
    const users = await User.findAll({
      attributes: { exclude: ['password'] } // Không trả về password
    });
    res.status(200).json({ success: true, data: users });
  } catch (error) {
    next(error); // Chuyển lỗi tới Error Handling Middleware tổng
  }
};

module.exports = {
  getAllUsers
};
