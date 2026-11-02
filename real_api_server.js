const express = require('express');
const cors = require('cors');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

// In-Memory Storage mô phỏng cấu hình từ Database thật
let gameConfig = {
  playerHealthBase: 120, // Máu nâng cấp từ Web: 120 HP (thay vì 100)
  walkSpeed: 0.55,
  runSpeed: 1.35,
  punchDamage: 20,       // Sát thương đấm từ Web: 20
  kickDamage: 30,        // Sát thương đá từ Web: 30
  enemyHealth: 60,       // Máu quái kiến từ Web: 60 HP (thay vì 50)
  enemyDamage: 18,       // Sát thương quái cắn từ Web: 18
  patrolSpeed: 0.15,
  chaseSpeed: 0.32,
  viewRadius: 0.70,      // Tầm nhìn 1/4 phòng
  viewAngle: 65,         // Góc FOV 65 độ
  hearingRadius: 1.10
};

// 1. Health check
app.get('/api/health', (req, res) => {
  res.json({
    status: 'success',
    message: 'Project Shrink Real API Server is running!',
    uptime: process.uptime()
  });
});

// 2. GET /api/v1/game/attributes (Unity Client gọi endpoint này khi Start)
app.get('/api/v1/game/attributes', (req, res) => {
  console.log(`[${new Date().toLocaleTimeString()}] 🎮 Unity Game Client đã gọi API /api/v1/game/attributes thành công!`);
  res.json({
    status: 'success',
    message: 'Game attributes loaded successfully from Web Admin.',
    data: gameConfig
  });
});

// 3. GET /api/v1/admin/config/character-attributes (Xem cấu hình Player)
app.get('/api/v1/admin/config/character-attributes', (req, res) => {
  res.json({
    status: 'success',
    data: {
      playerHealthBase: gameConfig.playerHealthBase,
      walkSpeed: gameConfig.walkSpeed,
      runSpeed: gameConfig.runSpeed,
      punchDamage: gameConfig.punchDamage,
      kickDamage: gameConfig.kickDamage
    }
  });
});

// 4. PUT /api/v1/admin/config/character-attributes (Web Admin chỉnh sửa Player)
app.put('/api/v1/admin/config/character-attributes', (req, res) => {
  Object.assign(gameConfig, req.body);
  console.log('📝 Web Admin đã cập nhật thông số Player:', req.body);
  res.json({
    status: 'success',
    message: 'Character attributes updated.',
    data: gameConfig
  });
});

// 5. GET /api/v1/admin/config/enemy-attributes (Xem cấu hình Quái)
app.get('/api/v1/admin/config/enemy-attributes', (req, res) => {
  res.json({
    status: 'success',
    data: {
      enemyHealth: gameConfig.enemyHealth,
      enemyDamage: gameConfig.enemyDamage,
      patrolSpeed: gameConfig.patrolSpeed,
      chaseSpeed: gameConfig.chaseSpeed,
      viewRadius: gameConfig.viewRadius,
      viewAngle: gameConfig.viewAngle,
      hearingRadius: gameConfig.hearingRadius
    }
  });
});

// 6. PUT /api/v1/admin/config/enemy-attributes (Web Admin chỉnh sửa Quái)
app.put('/api/v1/admin/config/enemy-attributes', (req, res) => {
  Object.assign(gameConfig, req.body);
  console.log('📝 Web Admin đã cập nhật thông số Quái:', req.body);
  res.json({
    status: 'success',
    message: 'Enemy attributes updated.',
    data: gameConfig
  });
});

app.listen(PORT, () => {
  console.log('====================================================');
  console.log(`🚀 REAL API SERVER ĐANG CHẠY TẠI: http://localhost:${PORT}`);
  console.log(`📡 URL API Unity đang chờ gọi: http://localhost:${PORT}/api/v1/game/attributes`);
  console.log('====================================================');
});
