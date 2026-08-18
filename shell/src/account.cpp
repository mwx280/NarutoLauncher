#include "account.h"

#include <QSettings>

namespace {

// QSettings 中的键名。账号以数组形式存储，避免动态键名导致的键冲突。
const QString kKeyAccountCount = "accounts/count";
const QString kKeyRemark = "accounts/%1/remark";
const QString kKeyUsername = "accounts/%1/username";
const QString kKeyPassword = "accounts/%1/password";
const QString kKeyAutoLogin = "accounts/%1/autoLogin";

}  // namespace

QVector<AccountInfo> AccountStore::LoadAll() const {
    QVector<AccountInfo> result;
    QSettings settings;
    const int count = settings.value(kKeyAccountCount, 0).toInt();
    for (int i = 0; i < count; ++i) {
        AccountInfo info;
        info.remark = settings.value(kKeyRemark.arg(i)).toString();
        info.username = settings.value(kKeyUsername.arg(i)).toString();
        info.password = settings.value(kKeyPassword.arg(i)).toString();
        info.autoLogin = settings.value(kKeyAutoLogin.arg(i), false).toBool();
        if (info.username.isEmpty())
            continue;  // 跳过异常空记录
        result.append(info);
    }
    return result;
}

void AccountStore::SaveAll(const QVector<AccountInfo>& accounts) const {
    QSettings settings;
    settings.clear();  // 清空旧数据，整体重写（简单可靠）
    settings.setValue(kKeyAccountCount, accounts.size());
    for (int i = 0; i < accounts.size(); ++i) {
        const AccountInfo& a = accounts.at(i);
        settings.setValue(kKeyRemark.arg(i), a.remark);
        settings.setValue(kKeyUsername.arg(i), a.username);
        settings.setValue(kKeyPassword.arg(i), a.password);
        settings.setValue(kKeyAutoLogin.arg(i), a.autoLogin);
    }
    settings.sync();
}
