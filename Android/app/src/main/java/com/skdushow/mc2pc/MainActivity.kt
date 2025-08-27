package com.skdushow.mc2pc

import android.content.Intent
import android.provider.Settings
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsControllerCompat
import kotlinx.coroutines.delay

@OptIn(ExperimentalMaterial3Api::class)
class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // 设置沉浸式状态栏 + 导航栏
        WindowCompat.setDecorFitsSystemWindows(window, false)

        val controller = WindowInsetsControllerCompat(window, window.decorView).apply {
            isAppearanceLightStatusBars = true
            isAppearanceLightNavigationBars = true
        }

        // 设置状态栏和导航栏透明
        window.statusBarColor = android.graphics.Color.TRANSPARENT
        window.navigationBarColor = android.graphics.Color.TRANSPARENT

        // 启动前台服务保活
        startKeepAliveService()

        setContent {
            MyApplicationTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    MainScreen()
                }
            }
        }
    }

    private fun startKeepAliveService() {
        try {
            val intent = Intent(this, KeepAliveService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(intent)
            } else {
                startService(intent)
            }
        } catch (e: Exception) {
            Log.e("MainActivity", "启动前台服务失败", e)
        }
    }

    fun openNotificationListenSettings() {
        try {
            val intent = Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS)
            startActivity(intent)
        } catch (e: Exception) {
            Toast.makeText(this, "无法打开设置，请手动前往：设置→通知→通知使用权", Toast.LENGTH_LONG).show()
        }
    }

    fun openAppSettings() {
        try {
            val intent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
                data = android.net.Uri.fromParts("package", packageName, null)
            }
            startActivity(intent)
        } catch (e: Exception) {
            Toast.makeText(this, "无法打开应用设置", Toast.LENGTH_SHORT).show()
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen() {
    val context = LocalContext.current
    val activity = context as? MainActivity
    
    var showContent by remember { mutableStateOf(false) }
    var showCards by remember { mutableStateOf(false) }
    
    LaunchedEffect(Unit) {
        showContent = true
        delay(300)
        showCards = true
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .padding(24.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState()),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Spacer(modifier = Modifier.height(48.dp))
            
            // 应用图标和标题区域
            AnimatedVisibility(
                visible = showContent,
                enter = fadeIn(animationSpec = tween(800)) + 
                        slideInVertically(
                            animationSpec = tween(800),
                            initialOffsetY = { -100 }
                        )
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    // 应用图标容器
                    Box(
                        modifier = Modifier
                            .size(120.dp)
                            .shadow(
                                elevation = 16.dp,
                                shape = RoundedCornerShape(30.dp),
                                spotColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.3f)
                            )
                            .background(
                                MaterialTheme.colorScheme.primaryContainer,
                                RoundedCornerShape(30.dp)
                            )
                            .padding(24.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Filled.MusicNote,
                            contentDescription = "应用图标",
                            modifier = Modifier.size(48.dp),
                            tint = MaterialTheme.colorScheme.onPrimaryContainer
                        )
                    }
                    
                    Spacer(modifier = Modifier.height(24.dp))
                    
                    Text(
                        text = stringResource(R.string.app_name),
                        style = MaterialTheme.typography.headlineLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onBackground,
                        textAlign = TextAlign.Center
                    )
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    Text(
                        text = stringResource(R.string.app_description),
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        textAlign = TextAlign.Center
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(48.dp))
            
            // 权限设置卡片区域
            AnimatedVisibility(
                visible = showCards,
                enter = fadeIn(animationSpec = tween(800, delayMillis = 300)) +
                        slideInVertically(
                            animationSpec = tween(800, delayMillis = 300),
                            initialOffsetY = { 100 }
                        )
            ) {
                PermissionSettingsCard(
                    onNotificationPermissionClick = { activity?.openNotificationListenSettings() },
                    onAppSettingsClick = { activity?.openAppSettings() }
                )
            }
            
            Spacer(modifier = Modifier.height(32.dp))
            
            // 使用说明卡片
            AnimatedVisibility(
                visible = showCards,
                enter = fadeIn(animationSpec = tween(800, delayMillis = 600)) +
                        slideInVertically(
                            animationSpec = tween(800, delayMillis = 600),
                            initialOffsetY = { 100 }
                        )
            ) {
                InstructionCard()
            }
            
            Spacer(modifier = Modifier.height(32.dp))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PermissionSettingsCard(
    onNotificationPermissionClick: () -> Unit,
    onAppSettingsClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .shadow(
                elevation = 8.dp,
                shape = RoundedCornerShape(24.dp),
                spotColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)
            ),
        shape = RoundedCornerShape(24.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface,
            contentColor = MaterialTheme.colorScheme.onSurface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(
            modifier = Modifier
                .padding(28.dp)
                .fillMaxWidth(),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // 标题
            Text(
                text = stringResource(R.string.permission_settings_title),
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(bottom = 24.dp)
            )
            
            // 通知权限设置
            PermissionItem(
                icon = Icons.Filled.Notifications,
                title = stringResource(R.string.notification_permission_title),
                description = stringResource(R.string.notification_permission_description),
                buttonText = stringResource(R.string.notification_permission_button),
                onClick = onNotificationPermissionClick,
                modifier = Modifier.padding(bottom = 20.dp)
            )
            
            // 分割线
            Divider(
                modifier = Modifier.padding(vertical = 8.dp),
                color = MaterialTheme.colorScheme.outlineVariant,
                thickness = 1.dp
            )
            
            // 应用设置
            PermissionItem(
                icon = Icons.Filled.Settings,
                title = stringResource(R.string.app_settings_title),
                description = stringResource(R.string.app_settings_description),
                buttonText = stringResource(R.string.app_settings_button),
                onClick = onAppSettingsClick
            )
        }
    }
}

@Composable
fun PermissionItem(
    icon: ImageVector,
    title: String,
    description: String,
    buttonText: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier.fillMaxWidth(),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // 图标容器
        Box(
            modifier = Modifier
                .size(48.dp)
                .background(
                    MaterialTheme.colorScheme.secondaryContainer,
                    RoundedCornerShape(12.dp)
                )
                .padding(12.dp),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                modifier = Modifier.size(24.dp),
                tint = MaterialTheme.colorScheme.onSecondaryContainer
            )
        }
        
        Spacer(modifier = Modifier.height(16.dp))
        
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth()
        )
        
        Spacer(modifier = Modifier.height(8.dp))
        
        Text(
            text = description,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth()
        )
        
        Spacer(modifier = Modifier.height(16.dp))
        
        Button(
            onClick = onClick,
            shape = RoundedCornerShape(12.dp),
            modifier = Modifier
                .fillMaxWidth()
                .height(48.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = MaterialTheme.colorScheme.primary,
                contentColor = MaterialTheme.colorScheme.onPrimary
            )
        ) {
            Text(
                text = buttonText,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

@Composable
fun InstructionCard() {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .shadow(
                elevation = 4.dp,
                shape = RoundedCornerShape(20.dp),
                spotColor = MaterialTheme.colorScheme.tertiary.copy(alpha = 0.1f)
            ),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.tertiaryContainer.copy(alpha = 0.3f),
            contentColor = MaterialTheme.colorScheme.onTertiaryContainer
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(
            modifier = Modifier
                .padding(24.dp)
                .fillMaxWidth()
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.padding(bottom = 16.dp)
            ) {
                Icon(
                    imageVector = Icons.Filled.Info,
                    contentDescription = null,
                    modifier = Modifier.size(24.dp),
                    tint = MaterialTheme.colorScheme.tertiary
                )
                Spacer(modifier = Modifier.width(12.dp))
                Text(
                    text = stringResource(R.string.instructions_title),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onTertiaryContainer
                )
            }
            
            InstructionStep(
                number = "1",
                text = stringResource(R.string.instruction_step_1)
            )
            InstructionStep(
                number = "2",
                text = stringResource(R.string.instruction_step_2)
            )
            InstructionStep(
                number = "3",
                text = stringResource(R.string.instruction_step_3)
            )
        }
    }
}

@Composable
fun InstructionStep(
    number: String,
    text: String
) {
    Row(
        verticalAlignment = Alignment.Top,
        modifier = Modifier.padding(vertical = 4.dp)
    ) {
        Box(
            modifier = Modifier
                .size(24.dp)
                .background(
                    MaterialTheme.colorScheme.tertiary,
                    RoundedCornerShape(12.dp)
                ),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = number,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onTertiary
            )
        }
        Spacer(modifier = Modifier.width(12.dp))
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onTertiaryContainer,
            modifier = Modifier.weight(1f)
        )
    }
}

@Composable
fun MyApplicationTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            // 使用系统的动态颜色（Android 12+）
            MaterialTheme.colorScheme
        } else {
            // 自定义颜色方案
            lightColorScheme(
                primary = Color(0xFF006C4C),
                onPrimary = Color(0xFFFFFFFF),
                primaryContainer = Color(0xFF89F8C7),
                onPrimaryContainer = Color(0xFF002114),
                secondary = Color(0xFF4C6358),
                onSecondary = Color(0xFFFFFFFF),
                secondaryContainer = Color(0xFFCFE9DB),
                onSecondaryContainer = Color(0xFF092017),
                tertiary = Color(0xFF3F6374),
                onTertiary = Color(0xFFFFFFFF),
                tertiaryContainer = Color(0xFFC3E8FC),
                onTertiaryContainer = Color(0xFF001F2A),
                background = Color(0xFFFBFDF9),
                onBackground = Color(0xFF191C1A),
                surface = Color(0xFFFBFDF9),
                onSurface = Color(0xFF191C1A),
                surfaceVariant = Color(0xFFDCE5DD),
                onSurfaceVariant = Color(0xFF404943),
                error = Color(0xFFBA1A1A),
                onError = Color(0xFFFFFFFF),
                errorContainer = Color(0xFFFFDAD6),
                onErrorContainer = Color(0xFF410002),
                outline = Color(0xFF707973),
                outlineVariant = Color(0xFFBFC9C2)
            )
        },
        typography = Typography(
            headlineLarge = Typography().headlineLarge.copy(
                fontSize = 32.sp,
                fontWeight = FontWeight.Bold
            ),
            headlineSmall = Typography().headlineSmall.copy(
                fontSize = 24.sp,
                fontWeight = FontWeight.Bold
            ),
            titleLarge = Typography().titleLarge.copy(
                fontSize = 22.sp,
                fontWeight = FontWeight.Bold
            ),
            titleMedium = Typography().titleMedium.copy(
                fontSize = 18.sp,
                fontWeight = FontWeight.SemiBold
            ),
            bodyLarge = Typography().bodyLarge.copy(
                fontSize = 16.sp
            ),
            bodyMedium = Typography().bodyMedium.copy(
                fontSize = 14.sp
            ),
            labelLarge = Typography().labelLarge.copy(
                fontSize = 14.sp,
                fontWeight = FontWeight.Medium
            )
        )
    ) {
        content()
    }
}
