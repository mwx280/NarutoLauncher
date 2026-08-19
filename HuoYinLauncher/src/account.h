#pragma once

#include <QString>
#include <QVector>

// 单个游戏账号信息。
// 说明：当前版本密码明文存储（使用系统级 QSettings，位于用户配置目录），
// 后续可升级为加密存储或系统凭据库（如 DPAPI / Windows Credential Manager）。
struct AccountInfo {
    QString remark;    // 备注名（可选，用于区分多个账号）
    QString username;  // 账号（QQ号）
    QString password;  // 密码
    bool scanLogin = false;   // 是否扫码登录（勾选后无需 QQ 号/密码，但备注必填）
};

// 账号存储管理：基于 QSettings 将账号列表持久化到本地。
//
// 存储位置：QSettings 默认路径（应用名定义于 main.cpp 的
// QCoreApplication::setApplicationName / setOrganizationName）。
// 组织为账号数组，支持增 / 改 / 删 / 查。
class AccountStore {
public:
    // 读取全部账号（未初始化时返回空列表）。
    QVector<AccountInfo> LoadAll() const;

    // 覆盖写入全部账号（调用方先 LoadAll 再修改后 SaveAll）。
    void SaveAll(const QVector<AccountInfo>& accounts) const;
};
