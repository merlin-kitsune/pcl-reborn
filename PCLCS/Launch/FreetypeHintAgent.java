package pcl.freetype;

import java.lang.instrument.ClassFileTransformer;
import java.lang.instrument.Instrumentation;
import java.security.ProtectionDomain;

import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/**
 * Runtime patch for lwjgl-freetype: force FT_LOAD_NO_HINTING on every glyph load.
 * The vulnerable TrueType bytecode interpreter is never entered, so a malicious
 * resource-pack font cannot execute its own bytecode.
 */
public final class FreetypeHintAgent {

    private static final String TARGET = "org/lwjgl/util/freetype/FreeType";
    private static final int FT_LOAD_NO_HINTING = 2;

    private FreetypeHintAgent() {
    }

    public static void premain(String agentArgs, Instrumentation instrumentation) {
        instrumentation.addTransformer(new ClassFileTransformer() {
            @Override
            public byte[] transform(ClassLoader loader, String className, Class<?> classBeingRedefined,
                                    ProtectionDomain protectionDomain, byte[] classfileBuffer) {
                if (!TARGET.equals(className)) return null;
                try {
                    return patch(classfileBuffer);
                } catch (Throwable t) {
                    System.err.println("[PCL] FreeType hinting patch failed: " + t);
                    t.printStackTrace();
                    // 补丁失败时必须中止，不能静默放行未受保护的 FreeType
                    Runtime.getRuntime().halt(1);
                    return null;
                }
            }
        });
        System.out.println("[PCL] FreeType hinting guard active (FT_LOAD_NO_HINTING)");
    }

    public static byte[] patch(byte[] classfileBuffer) {
        ClassReader reader = new ClassReader(classfileBuffer);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        final int[] patched = { 0 };
        ClassVisitor visitor = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                final int flagLocal = flagLocal(name, descriptor);
                if (flagLocal < 0) return mv;
                return new MethodVisitor(Opcodes.ASM9, mv) {
                    @Override
                    public void visitCode() {
                        super.visitCode();
                        patched[0]++;
                        mv.visitVarInsn(Opcodes.ILOAD, flagLocal);
                        mv.visitInsn(Opcodes.ICONST_2);
                        mv.visitInsn(Opcodes.IOR);
                        mv.visitVarInsn(Opcodes.ISTORE, flagLocal);
                    }
                };
            }
        };
        reader.accept(visitor, 0);
        if (patched[0] != 4) {
            throw new IllegalStateException("FreeType hinting guard found " + patched[0] + " of 4 expected load methods");
        }
        return writer.toByteArray();
    }

    private static int flagLocal(String name, String descriptor) {
        if ("FT_Load_Glyph".equals(name) && "(Lorg/lwjgl/util/freetype/FT_Face;II)I".equals(descriptor)) return 2;
        if ("FT_Load_Char".equals(name) && "(Lorg/lwjgl/util/freetype/FT_Face;JI)I".equals(descriptor)) return 3;
        if ("nFT_Load_Glyph".equals(name) && "(JII)I".equals(descriptor)) return 3;
        if ("nFT_Load_Char".equals(name) && "(JJI)I".equals(descriptor)) return 4;
        return -1;
    }
}