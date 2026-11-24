import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { reportError } from '@/lib/crash';
import { colors, fonts } from '@/lib/theme';

type Props = { children: ReactNode };
type State = { error: Error | null };

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.warn('Nereye UI', error, info.componentStack);
    reportError(error, { componentStack: info.componentStack ?? undefined });
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }
    return (
      <View style={styles.wrap}>
        <Text style={styles.h}>Bir şey kaçtı</Text>
        <Text style={styles.p}>Ekranı yenile, konum veya niyeti değiştir.</Text>
        <Pressable
          style={styles.btn}
          onPress={() => this.setState({ error: null })}
          accessibilityRole="button"
          accessibilityLabel="Yeniden dene">
          <Text style={styles.btnText}>Yeniden dene</Text>
        </Pressable>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  wrap: { flex: 1, backgroundColor: colors.bg, alignItems: 'center', justifyContent: 'center', padding: 24 },
  h: { fontFamily: fonts.display, fontSize: 28, color: colors.ink },
  p: { color: colors.muted, marginTop: 8, textAlign: 'center' },
  btn: { marginTop: 16, minHeight: 44, justifyContent: 'center', backgroundColor: colors.coral, paddingHorizontal: 18, paddingVertical: 12, borderRadius: 16 },
  btnText: { color: colors.white, fontWeight: '800' },
});
